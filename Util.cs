using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace WfcPatcher {
	public static class Util {

		#region SwapEndian
		public static Int16 SwapEndian( this Int16 x ) {
			return (Int16)SwapEndian( (UInt16)x );
		}
		public static UInt16 SwapEndian( this UInt16 x ) {
			return x = (UInt16)
					   ( ( x << 8 ) |
						( x >> 8 ) );
		}

		public static Int32 SwapEndian( this Int32 x ) {
			return (Int32)SwapEndian( (UInt32)x );
		}
		public static UInt32 SwapEndian( this UInt32 x ) {
			return x = ( x << 24 ) |
					  ( ( x << 8 ) & 0x00FF0000 ) |
					  ( ( x >> 8 ) & 0x0000FF00 ) |
					   ( x >> 24 );
		}

		public static Int64 SwapEndian( this Int64 x ) {
			return (Int64)SwapEndian( (UInt64)x );
		}
		public static UInt64 SwapEndian( this UInt64 x ) {
			return x = ( x << 56 ) |
						( ( x << 40 ) & 0x00FF000000000000 ) |
						( ( x << 24 ) & 0x0000FF0000000000 ) |
						( ( x << 8 ) & 0x000000FF00000000 ) |
						( ( x >> 8 ) & 0x00000000FF000000 ) |
						( ( x >> 24 ) & 0x0000000000FF0000 ) |
						( ( x >> 40 ) & 0x000000000000FF00 ) |
						 ( x >> 56 );
		}
		#endregion

		#region HexUtils
		public static byte ParseDecOrHexToByte( string s ) {
			s = s.Trim();

			if ( s.StartsWith( "0x" ) ) {
				s = s.Substring( 2 );
				return Byte.Parse( s, System.Globalization.NumberStyles.HexNumber );
			} else {
				return Byte.Parse( s );
			}
		}

		public static byte[] HexStringToByteArray( string hex ) {
			if ( hex.Length % 2 == 1 )
				throw new Exception( "The binary key cannot have an odd number of digits" );

			byte[] arr = new byte[hex.Length >> 1];

			for ( int i = 0; i < hex.Length >> 1; ++i ) {
				arr[i] = (byte)( ( GetHexVal( hex[i << 1] ) << 4 ) + ( GetHexVal( hex[( i << 1 ) + 1] ) ) );
			}

			return arr;
		}

		public static int GetHexVal( char hex ) {
			int val = (int)hex;
			//For uppercase A-F letters:
			//return val - (val < 58 ? 48 : 55);
			//For lowercase a-f letters:
			//return val - (val < 58 ? 48 : 87);
			//Or the two combined, but a bit slower:
			return val - ( val < 58 ? 48 : ( val < 97 ? 55 : 87 ) );
		}
		#endregion

		#region NumberUtils
		public static uint ToUInt24( byte[] File, int Pointer ) {
			byte b1 = File[Pointer];
			byte b2 = File[Pointer + 1];
			byte b3 = File[Pointer + 2];

			return (uint)( b3 << 16 | b2 << 8 | b1 );
		}
		public static byte[] GetBytesForUInt24( uint Number ) {
			byte[] b = new byte[3];
			b[0] = (byte)( Number & 0xFF );
			b[1] = (byte)( ( Number >> 8 ) & 0xFF );
			b[2] = (byte)( ( Number >> 16 ) & 0xFF );
			return b;
		}

		/// <summary>
		/// converts a 32-bit int that's actually a byte representation of a float
		/// to an actual float for use in calculations or whatever
		/// </summary>
		public static float UIntToFloat( uint integer ) {
			byte[] b = BitConverter.GetBytes( integer );
			float f = BitConverter.ToSingle( b, 0 );
			return f;
		}

		public static int Align( this int Number, int Alignment ) {
			return (int)Align( (uint)Number, (uint)Alignment );
		}
		public static uint Align( this uint Number, uint Alignment ) {
			uint diff = Number % Alignment;
			if ( diff == 0 ) {
				return Number;
			} else {
				return ( Number + ( Alignment - diff ) );
			}
		}

		#endregion

		#region TextUtils
		private static Encoding _ShiftJISEncoding = null;
		public static Encoding ShiftJISEncoding { get { if ( _ShiftJISEncoding == null ) { _ShiftJISEncoding = Encoding.GetEncoding( 932 ); } return _ShiftJISEncoding; } }
		public static String GetTextShiftJis( byte[] File, int Pointer ) {
			if ( Pointer == -1 ) return null;

			try {
				int i = Pointer;
				while ( File[i] != 0x00 ) {
					i++;
				}
				String Text = ShiftJISEncoding.GetString( File, Pointer, i - Pointer );
				return Text;
			} catch ( Exception ) {
				return null;
			}
		}
		public static String GetTextAscii( byte[] File, int Pointer ) {
			if ( Pointer == -1 ) return null;

			try {
				int i = Pointer;
				while ( File[i] != 0x00 ) {
					i++;
				}
				String Text = Encoding.ASCII.GetString( File, Pointer, i - Pointer );
				return Text;
			} catch ( Exception ) {
				return null;
			}
		}
		public static String GetTextUnicode( byte[] File, int Pointer, int MaxByteLength ) {
			StringBuilder sb = new StringBuilder();
			for ( int i = 0; i < MaxByteLength; i += 2 ) {
				ushort ch = BitConverter.ToUInt16( File, Pointer + i );
				if ( ch == 0 || ch == 0xFFFF ) { break; }
				sb.Append( (char)ch );
			}
			return sb.ToString();
		}
		public static String GetTextUTF8( byte[] File, int Pointer ) {
			int tmp;
			return GetTextUTF8( File, Pointer, out tmp );
		}
		public static String GetTextUTF8( byte[] File, int Pointer, out int NullLocation ) {
			if ( Pointer == -1 ) { NullLocation = -1; return null; }

			try {
				int i = Pointer;
				while ( File[i] != 0x00 ) {
					i++;
				}
				String Text = Encoding.UTF8.GetString( File, Pointer, i - Pointer );
				NullLocation = i;
				return Text;
			} catch ( Exception ) {
				NullLocation = -1;
				return null;
			}
		}
		public static String TrimNull( this String s ) {
			int n = s.IndexOf( '\0', 0 );
			if ( n >= 0 ) {
				return s.Substring( 0, n );
			}
			return s;
		}
		public static byte[] StringToBytesShiftJis( String s ) {
			//byte[] bytes = ShiftJISEncoding.GetBytes(s);
			//return bytes.TakeWhile(subject => subject != 0x00).ToArray();
			return ShiftJISEncoding.GetBytes( s );
		}
		public static byte[] StringToBytesUTF16( String s ) {
			return Encoding.Unicode.GetBytes( s );
		}

		public static string XmlEscape( string s ) {
			s = s.Replace( "&", "&amp;" );
			s = s.Replace( "\"", "&quot;" );
			s = s.Replace( "'", "&apos;" );
			s = s.Replace( "<", "&lt;" );
			s = s.Replace( ">", "&gt;" );
			return s;
		}
		#endregion

		#region TimeUtils
		public static DateTime UnixTimeToDateTime( ulong unixTime ) {
			return new DateTime( 1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc ).AddSeconds( unixTime ).ToLocalTime();
		}
		public static ulong DateTimeToUnixTime( DateTime time ) {
			return (ulong)( time - new DateTime( 1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc ).ToLocalTime() ).TotalSeconds;
		}
		public static DateTime PS3TimeToDateTime( ulong PS3Time ) {
			return new DateTime( 1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc ).AddMilliseconds( PS3Time / 1000 ).ToLocalTime();
		}
		#endregion

		#region ProgramUtils
		public static bool RunProgram( String prog, String args, bool displayCommandLine, bool displayOutput, bool useShell = false ) {
			if ( displayCommandLine ) {
				Console.Write( prog );
				Console.Write( " " );
				Console.WriteLine( args );
			}

			// Use ProcessStartInfo class
			System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
			startInfo.CreateNoWindow = false;
			startInfo.UseShellExecute = useShell;
			startInfo.FileName = prog;
			startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
			startInfo.Arguments = args;
			startInfo.RedirectStandardOutput = !useShell;
			startInfo.RedirectStandardError = !useShell;
			//startInfo.RedirectStandardInput = !useShell;
			//startInfo.UserName = System.Security.Principal.WindowsIdentity.GetCurrent().Name;

			using ( System.Diagnostics.Process exeProcess = System.Diagnostics.Process.Start( startInfo ) ) {
				exeProcess.WaitForExit();
				if ( useShell ) {
					return exeProcess.ExitCode == 0;
				}

				string output;
				string err;
				output = exeProcess.StandardOutput.ReadToEnd();
				err = exeProcess.StandardError.ReadToEnd();
				int exitCode = exeProcess.ExitCode;

				if ( exitCode != 0 ) {
					Console.WriteLine( prog + " returned nonzero:" );
					Console.WriteLine( output );
					throw new Exception( output );
					//return false;
				}

				if ( displayOutput ) {
					Console.WriteLine( output );
					Console.WriteLine( err );
				}

				return true;
			}
		}

		#endregion

		#region ArrayUtils
		public static void CopyByteArrayPart( IList<byte> from, int locationFrom, IList<byte> to, int locationTo, int count ) {
			for ( int i = 0; i < count; i++ ) {
				to[locationTo + i] = from[locationFrom + i];
			}
		}

		public static void FillNull( IList<byte> Array, int Location, int Count ) {
			for ( int i = 0; i < Count; ++i ) {
				Array[Location + i] = 0x00;
			}
		}

		public static bool IsByteArrayPartEqual( IList<byte> Array1, int Location1, IList<byte> Array2, int Location2, int count ) {
			for ( int i = 0; i < count; ++i ) {
				if ( Array1[i + Location1] != Array2[i + Location2] ) {
					return false;
				}
			}
			return true;
		}
		#endregion

		#region StreamUtils
		public static void CopyStream( System.IO.Stream input, System.IO.Stream output, int count ) {
			byte[] buffer = new byte[4096];
			int read;

			int bytesLeft = count;
			while ( ( read = input.Read( buffer, 0, Math.Min( buffer.Length, bytesLeft ) ) ) > 0 ) {
				output.Write( buffer, 0, read );
				bytesLeft -= read;
				if ( bytesLeft <= 0 ) return;
			}
		}

		public static uint ReadUInt32( this Stream s ) {
			int b1 = s.ReadByte();
			int b2 = s.ReadByte();
			int b3 = s.ReadByte();
			int b4 = s.ReadByte();

			return (uint)( b4 << 24 | b3 << 16 | b2 << 8 | b1 );
		}
		public static uint PeekUInt32( this Stream s ) {
			long pos = s.Position;
			uint retval = s.ReadUInt32();
			s.Position = pos;
			return retval;
		}
		public static uint ReadUInt24( this Stream s ) {
			int b1 = s.ReadByte();
			int b2 = s.ReadByte();
			int b3 = s.ReadByte();

			return (uint)( b3 << 16 | b2 << 8 | b1 );
		}
		public static uint PeekUInt24( this Stream s ) {
			long pos = s.Position;
			uint retval = s.ReadUInt24();
			s.Position = pos;
			return retval;
		}
		public static ushort ReadUInt16( this Stream s ) {
			int b1 = s.ReadByte();
			int b2 = s.ReadByte();

			return (ushort)( b2 << 8 | b1 );
		}
		public static ushort PeekUInt16( this Stream s ) {
			long pos = s.Position;
			ushort retval = s.ReadUInt16();
			s.Position = pos;
			return retval;
		}

		public static string ReadAsciiNullterm( this Stream s ) {
			StringBuilder sb = new StringBuilder();
			int b = s.ReadByte();
			while ( b != 0 && b != -1 ) {
				sb.Append( (char)( b ) );
				b = s.ReadByte();
			}
			return sb.ToString();
		}
		public static string ReadAscii( this Stream s, int count ) {
			StringBuilder sb = new StringBuilder( count );
			int b;
			for ( int i = 0; i < count; ++i ) {
				b = s.ReadByte();
				sb.Append( (char)( b ) );
			}
			return sb.ToString();
		}
		public static string ReadUTF16Nullterm( this Stream s ) {
			StringBuilder sb = new StringBuilder();
			byte[] b = new byte[2];
			int b0 = s.ReadByte();
			int b1 = s.ReadByte();
			while ( !( b0 == 0 && b1 == 0 ) && b1 != -1 ) {
				b[0] = (byte)b0; b[1] = (byte)b1;
				sb.Append( Encoding.Unicode.GetString( b, 0, 2 ) );
				b0 = s.ReadByte(); b1 = s.ReadByte();
			}
			return sb.ToString();
		}
		#endregion

		public static string GuessFileExtension( Stream s ) {
			uint magic32 = s.PeekUInt32();
			uint magic24 = s.PeekUInt24();
			uint magic16 = s.PeekUInt16();

			switch ( magic32 ) {
				case 0x46464952:
					return ".wav";
				case 0x474E5089:
					return ".png";
				case 0x5367674F:
					return ".ogg";
			}
			switch ( magic16 ) {
				case 0x4D42:
					return ".bmp";
			}

			return "";
		}

	}
}
