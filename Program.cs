using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WfcPatcher {
	class Program {
		static void Main( string[] args ) {
			foreach ( string s in args ) {
#if DEBUG
#else
				try {
#endif
					string newFilename = PatchFile( s );
					Console.WriteLine( "Patched to " + newFilename + "!" );
					Console.WriteLine();
#if DEBUG
#else
				} catch ( Exception ex ) {
					Console.WriteLine( "Failed patching " + s );
					Console.WriteLine( ex.ToString() );
					Console.WriteLine();
				}
#endif
			}
		}

		static string PatchFile( string filename ) {
			Console.WriteLine( "Reading and copying " + filename + "..." );
			var ndsSrc = new System.IO.FileStream( filename, System.IO.FileMode.Open );
			string newFilename = System.IO.Path.Combine( System.IO.Path.GetDirectoryName( filename ), System.IO.Path.GetFileNameWithoutExtension( filename ) ) + " (AltWfc)" + System.IO.Path.GetExtension( filename );
			var nds = new System.IO.FileStream( newFilename, System.IO.FileMode.Create );
			Util.CopyStream( ndsSrc, nds, (int)ndsSrc.Length );
			ndsSrc.Close();

			// http://dsibrew.org/wiki/DSi_Cartridge_Header

			// arm
			Console.WriteLine( "Patching ARM Executables..." );
			nds.Position = 0x20;
			uint arm9offset = nds.ReadUInt32();
			uint arm9entry = nds.ReadUInt32();
			uint arm9load = nds.ReadUInt32();
			uint arm9size = nds.ReadUInt32();
			uint arm7offset = nds.ReadUInt32();
			uint arm7entry = nds.ReadUInt32();
			uint arm7load = nds.ReadUInt32();
			uint arm7size = nds.ReadUInt32();

			PatchArm9( nds, arm9offset, arm9size );
			PatchArm7( nds, arm7offset, arm7size );

			// overlays
			Console.WriteLine( "Patching Overlays..." );
			nds.Position = 0x50;
			uint arm9overlayoff = nds.ReadUInt32();
			uint arm9overlaylen = nds.ReadUInt32();
			uint arm7overlayoff = nds.ReadUInt32();
			uint arm7overlaylen = nds.ReadUInt32();

			PatchOverlay( nds, arm9overlayoff, arm9overlaylen );
			PatchOverlay( nds, arm7overlayoff, arm7overlaylen );

			nds.Close();

			return newFilename;
		}

		static void PatchArm9( System.IO.FileStream nds, uint pos, uint len ) {
			nds.Position = pos;
			byte[] data = new byte[len];
			nds.Read( data, 0, (int)len );

			// decompress size info: http://www.crackerscrap.com/docs/dsromstructure.html
			// TODO: Is there a better way to figure out if an ARM9 is compressed?

			nds.Position = nds.Position - 8;
			uint compressedSize = nds.ReadUInt24();
			nds.ReadByte();
			uint additionalCompressedSize = nds.ReadUInt32();
			uint decompressedSize = additionalCompressedSize + len;

			bool compressed = false;
			byte[] decData = data;

#if DEBUG
			Console.WriteLine( "ARM9 old dec size: 0x" + decompressedSize.ToString( "X6" ) );
			Console.WriteLine( "ARM9 old cmp size: 0x" + compressedSize.ToString( "X6" ) );
			Console.WriteLine( "ARM9 old filesize: 0x" + len.ToString( "X6" ) );
			Console.WriteLine( "ARM9 old diff:     0x" + additionalCompressedSize.ToString( "X6" ) );
#endif

			blz blz = new blz();
			// if this condition isn't true then it can't be blz-compressed so don't even try
			if ( data.Length == compressedSize + 0x4000 || data.Length == compressedSize + 0x4004 ) {
				try {
					blz.arm9 = 1;
					byte[] maybeDecData = blz.BLZ_Decode( data );

					if ( maybeDecData.Length == decompressedSize ) {
						compressed = true;
						decData = maybeDecData;
					}
				} catch ( Exception ) {
					compressed = false;
				}
			}

			byte[] decDataUnmodified = (byte[])decData.Clone();
			if ( ReplaceInData( decData, 0x00, true ) ) {
				if ( compressed ) {
					Console.WriteLine( "Replacing and recompressing ARM9..." );
					data = blz.BLZ_Encode( decData, 0 );

					uint newCompressedSize = (uint)data.Length;
					if ( newCompressedSize > len ) {
						// new ARM is actually bigger, redo without the additional nullterm replacement
						decData = decDataUnmodified;
						ReplaceInData( decData, 0x00, false );
						data = blz.BLZ_Encode( decData, 0 );
						newCompressedSize = (uint)data.Length;

						int arm9diff = (int)len - (int)newCompressedSize;
						if ( arm9diff < 0 ) {
							Console.WriteLine( "WARNING: Recompressed ARM9 is " + -arm9diff + " bytes bigger than original!" );
							Console.WriteLine( "         Patched game may be corrupted!" );
						}
					}

					if ( newCompressedSize != len ) {
						// new ARM is (still) different, attempt to find the metadata in the ARM9 secure area and replace that
						byte[] newCmpSizeBytes = BitConverter.GetBytes( newCompressedSize );
						for ( int i = 0; i < 0x4000; i += 4 ) {
							uint maybeSize = BitConverter.ToUInt32( data, i );
							if ( maybeSize == len + 0x02000000 ) {
								data[i + 0] = newCmpSizeBytes[0];
								data[i + 1] = newCmpSizeBytes[1];
								data[i + 2] = newCmpSizeBytes[2];
								data[i + 3] = (byte)( newCmpSizeBytes[3] + 0x02 );
								break;
							}
						}
					}
#if DEBUG
					uint newDecompressedSize = (uint)decData.Length;
					uint newAdditionalCompressedSize = newDecompressedSize - newCompressedSize;
					Console.WriteLine( "ARM9 new dec size: 0x" + newDecompressedSize.ToString( "X6" ) );
					Console.WriteLine( "ARM9 new cmp size: 0x" + newCompressedSize.ToString( "X6" ) );
					Console.WriteLine( "ARM9 new diff:     0x" + newAdditionalCompressedSize.ToString( "X6" ) );
#endif
				} else {
					Console.WriteLine( "Replacing ARM9..." );
					data = decData;
				}

				nds.Position = pos;
				nds.Write( data, 0, data.Length );

				int newSize = data.Length;
				int diff = (int)len - newSize;
				
				// copy back footer
				if ( diff > 0 ) {
					List<byte> footer = new List<byte>();
					nds.Position = pos + len;
					if ( nds.PeekUInt32() == 0xDEC00621 ) {
						for ( int j = 0; j < 12; ++j ) {
							footer.Add( (byte)nds.ReadByte() );
						}

						nds.Position = pos + newSize;
						nds.Write( footer.ToArray(), 0, footer.Count );
					}

					// padding
					for ( int j = 0; j < diff; ++j ) {
						nds.WriteByte( 0xFF );
					}
				}

				// write new size
				byte[] newSizeBytes = BitConverter.GetBytes( newSize );
				nds.Position = 0x2C;
				nds.Write( newSizeBytes, 0, 4 );

				// recalculate checksums
				nds.Position = pos;
				ushort secureChecksum = new Crc16().ComputeChecksum( nds, 0x4000, 0xFFFF );
				nds.Position = 0x6C;
				nds.Write( BitConverter.GetBytes( secureChecksum ), 0, 2 );

				nds.Position = 0;
				ushort headerChecksum = new Crc16().ComputeChecksum( nds, 0x15E, 0xFFFF );
				nds.Write( BitConverter.GetBytes( headerChecksum ), 0, 2 );
			}
		}

		static void PatchArm7( System.IO.FileStream nds, uint pos, uint len ) {
			nds.Position = pos;
			byte[] data = new byte[len];
			nds.Read( data, 0, (int)len );

			if ( ReplaceInData( data ) ) {
				Console.WriteLine( "Replacing ARM7..." );
				nds.Position = pos;
				nds.Write( data, 0, data.Length );
			}

			return;
		}

		static void PatchOverlay( System.IO.FileStream nds, uint pos, uint len ) {
			// http://sourceforge.net/p/devkitpro/ndstool/ci/master/tree/source/ndsextract.cpp
			// http://sourceforge.net/p/devkitpro/ndstool/ci/master/tree/source/overlay.h
			// header compression info from http://gbatemp.net/threads/recompressing-an-overlay-file.329576/

			nds.Position = 0x048;
			uint fatOffset = nds.ReadUInt32();

			for ( uint i = 0; i < len; i += 0x20 ) {
				nds.Position = pos + i;
				uint id = nds.ReadUInt32();
				uint ramAddr = nds.ReadUInt32();
				uint ramSize = nds.ReadUInt32();
				uint bssSize = nds.ReadUInt32();
				uint sinitInit = nds.ReadUInt32();
				uint sinitInitEnd = nds.ReadUInt32();
				uint fileId = nds.ReadUInt32();
				uint compressedSize = nds.ReadUInt24();
				byte compressedBitmask = (byte)nds.ReadByte();

				nds.Position = fatOffset + 8 * id;
				uint overlayPositionStart = nds.ReadUInt32();
				uint overlayPositionEnd = nds.ReadUInt32();
				uint overlaySize = overlayPositionEnd - overlayPositionStart;

				if ( overlaySize == 0 ) { continue; }

				nds.Position = overlayPositionStart;
				byte[] data = new byte[overlaySize];
				nds.Read( data, 0, (int)overlaySize );

				blz blz = new blz();
				byte[] decData;

				bool compressed = ( compressedBitmask & 0x01 ) == 0x01;
				if ( compressed ) {
					decData = blz.BLZ_Decode( data );
				} else {
					decData = data;
				}


				if ( ReplaceInData( decData ) ) {
					int newOverlaySize;
					int diff;

					// if something was replaced, put it back into the ROM
					if ( compressed ) {
						Console.WriteLine( "Replacing and recompressing overlay " + id + "..." );

						uint newCompressedSize = 0;
						data = blz.BLZ_Encode( decData, 0 );
						newCompressedSize = (uint)data.Length;

						newOverlaySize = data.Length;
						diff = (int)overlaySize - newOverlaySize;

						if ( diff < 0 ) {
							Console.WriteLine( "Removing known debug strings and recompressing overlay " + id + "..." );
							decData = RemoveDebugStrings( decData );
							data = blz.BLZ_Encode( decData, 0 );
							newCompressedSize = (uint)data.Length;

							newOverlaySize = data.Length;
							diff = (int)overlaySize - newOverlaySize;
							if ( diff < 0 ) {
								Console.WriteLine( "WARNING: Recompressed overlay is " + -diff + " bytes bigger than original!" );
								Console.WriteLine( "         Patched game may be corrupted!" );
							}
						}

						// replace compressed size, if it was used before
						if ( compressedSize == overlaySize ) {
							byte[] newCompressedSizeBytes = BitConverter.GetBytes( newCompressedSize );
							nds.Position = pos + i + 0x1C;
							nds.Write( newCompressedSizeBytes, 0, 3 );
						}

					} else {
						Console.WriteLine( "Replacing overlay " + id + "..." );

						data = decData;
					}

					newOverlaySize = data.Length;
					diff = (int)overlaySize - newOverlaySize;

					nds.Position = overlayPositionStart;
					nds.Write( data, 0, data.Length );

					overlayPositionEnd = (uint)nds.Position;

					// padding
					for ( int j = 0; j < diff; ++j ) {
						nds.WriteByte( 0xFF );
					}

					// new file end offset
					byte[] newPosEndData = BitConverter.GetBytes( overlayPositionEnd );
					nds.Position = fatOffset + 8 * id + 4;
					nds.Write( newPosEndData, 0, 4 );
				}

			}
		}

		static byte[] RemoveDebugStrings( byte[] data ) {
			string[] debugStrings = new string[] {
				"recv buffer size",
				"send buffer size",
				"unknown connect mode",
				"Split packet parse error",
				"NULL byte expected!",
				"Processing adderror packet",
				"Out of memory.",
			};

			foreach ( string s in debugStrings ) {
				byte[] searchBytes = Encoding.ASCII.GetBytes( s );
				var results = data.Locate( searchBytes );

				foreach ( int result in results ) {
					for ( int i = 0; i < searchBytes.Length; ++i ) {
						data[result + i] = 0x20;
					}
				}
			}

			return data;
		}

		static bool ReplaceInData( byte[] data, byte paddingByte = 0x00, bool writeAdditionalBytePostString = false ) {
			bool replacedData = false;
			string search = "https://";
			string replace = "http://";
			byte[] searchBytes = Encoding.ASCII.GetBytes( search );
			byte[] replaceBytes = Encoding.ASCII.GetBytes( replace );
			int requiredPadding = searchBytes.Length - replaceBytes.Length;

			var results = data.Locate( searchBytes );
			if ( results.Length == 0 ) {
				return false;
			}

			foreach ( int result in results ) {
				string originalString = Util.GetTextAscii( data, result );
#if DEBUG
				Console.WriteLine( originalString );
#endif
				if ( originalString == "https://" ) { continue; } // don't replace lone https, probably used for strcmp to figure out if an URL is SSL or not
				string replacedString = originalString.Replace( search, replace );
				byte[] replacedStringBytes = Encoding.ASCII.GetBytes( replacedString );

				replacedData = true;

				int i = 0;
				for ( ; i < replacedStringBytes.Length; ++i ) {
					data[result + i] = replacedStringBytes[i];
				}
				for ( ; i < replacedStringBytes.Length + requiredPadding; ++i ) {
					data[result + i] = paddingByte;
				}

				// Alright, this might require some explaination.
				// This is putting a byte in the location that previously held the NULL terminator at the end of the string.
				// Thanks to "http" being one byte shorter than "https", the new NULL terminator was just placed in the
				// padding loop above, and the byte below, at [result + i], is unused. Thus, we can just place anything in
				// there without affecting the program. Now, the actual *reason* we're putting a byte in here is to reduce
				// the chance of the recompressed binary becoming smaller than the original one. We want it to remain the
				// exact same size. Now, of course, this is not always going to happen, but this should improve the chance
				// significantly.
				if ( writeAdditionalBytePostString ) {
					data[result + i] = 0x7F;
				}
			}

			return replacedData;
		}
	}
}
