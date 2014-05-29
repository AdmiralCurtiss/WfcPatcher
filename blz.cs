using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WfcPatcher {
	class blz {
		/*----------------------------------------------------------------------------*/
		/*--  blz.c - Bottom LZ coding for Nintendo GBA/DS                          --*/
		/*--  Copyright (C) 2011 CUE                                                --*/
		/*--                                                                        --*/
		/*--  This program is free software: you can redistribute it and/or modify  --*/
		/*--  it under the terms of the GNU General Public License as published by  --*/
		/*--  the Free Software Foundation, either version 3 of the License, or     --*/
		/*--  (at your option) any later version.                                   --*/
		/*--                                                                        --*/
		/*--  This program is distributed in the hope that it will be useful,       --*/
		/*--  but WITHOUT ANY WARRANTY; without even the implied warranty of        --*/
		/*--  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the          --*/
		/*--  GNU General Public License for more details.                          --*/
		/*--                                                                        --*/
		/*--  You should have received a copy of the GNU General Public License     --*/
		/*--  along with this program. If not, see <http://www.gnu.org/licenses/>.  --*/
		/*----------------------------------------------------------------------------*/

		/*----------------------------------------------------------------------------*/
		public const uint CMD_DECODE = 0x00;       // decode
		public const uint CMD_ENCODE = 0x01;       // encode

		public const uint BLZ_NORMAL = 0;          // normal mode
		public const uint BLZ_BEST = 1;          // best mode

		public const uint BLZ_SHIFT = 1;          // bits to shift
		public const byte BLZ_MASK = 0x80;       // bits to check:
		// ((((1 << BLZ_SHIFT) - 1) << (8 - BLZ_SHIFT)

		public const uint BLZ_THRESHOLD = 2;          // max number of bytes to not encode
		public const uint BLZ_N = 0x1002;     // max offset ((1 << 12) + 2)
		public const uint BLZ_F = 0x12;       // max coded ((1 << 4) + BLZ_THRESHOLD)

		public const uint RAW_MINIM = 0x00000000; // empty file, 0 bytes
		public const uint RAW_MAXIM = 0x00FFFFFF; // 3-bytes length, 16MB - 1

		public const uint BLZ_MINIM = 0x00000004; // header only (empty RAW file)
		public const uint BLZ_MAXIM = 0x01400000; // 0x0120000A, padded to 20MB:
		// * length, RAW_MAXIM
		// * flags, (RAW_MAXIM + 7) / 8
		// * header, 11
		// 0x00FFFFFF + 0x00200000 + 12 + padding

		/*----------------------------------------------------------------------------*/
		public int arm9;
		public bool fileWasNotCompressed = false;

		/*----------------------------------------------------------------------------*/
		/*#define BREAK(text)   { printf(text); return; }
		#define EXIT(text)    { printf(text); exit(-1); }

		/*----------------------------------------------------------------------------*/
		/*int main(int argc, char **argv) {
		  int cmd, mode;
		  int arg;

		  Title();

		  if (argc < 2) Usage();
		  if      (!strcmpi(argv[1], "-d"))   { cmd = CMD_DECODE; }
		  else if (!strcmpi(argv[1], "-en"))  { cmd = CMD_ENCODE; mode = BLZ_NORMAL; }
		  else if (!strcmpi(argv[1], "-eo"))  { cmd = CMD_ENCODE; mode = BLZ_BEST; }
		  else if (!strcmpi(argv[1], "-en9")) { cmd = CMD_ENCODE; mode = BLZ_NORMAL; }
		  else if (!strcmpi(argv[1], "-eo9")) { cmd = CMD_ENCODE; mode = BLZ_BEST; }
		  else                                 EXIT("Command not supported\n");
		  if (argc < 3) EXIT("Filename not specified\n");

		  switch (cmd) {
			case CMD_DECODE:
			  for (arg = 2; arg < argc; arg++) BLZ_Decode(argv[arg]);
			  break;
			case CMD_ENCODE:
			  arm9 = argv[1][3] == '9' ? 1 : 0;
			  for (arg = 2; arg < argc; arg++) BLZ_Encode(argv[arg], mode);
			  break;
			default:
			  break;
		  }

		  printf("\nDone\n");

		  return(0);
		}

		/*----------------------------------------------------------------------------*/
		/*void Title() {
		  printf(
			"\n"
			"BLZ - (c) CUE 2011\n"
			"Bottom LZ coding for Nintendo GBA/DS\n"
			"\n"
		  ); 
		}

		/*----------------------------------------------------------------------------*/
		/*void Usage() {
		  EXIT(
			"Usage: BLZ command filename [filename [...]]\n"
			"\n"
			"command:\n"
			"  -d ....... decode 'filename'\n"
			"  -en[9] ... encode 'filename', normal mode\n"
			"  -eo[9] ... encode 'filename', optimal mode (LZ-CUE)\n"
			"\n"
			"* '9' compress an ARM9 file with 0x4000 bytes decoded\n"
			"* multiple filenames and wildcards are permitted\n"
			"* the original file is overwritten with the new file\n"
			"* this codification is used in the DS overlay files\n"
		  );
		}

		/*----------------------------------------------------------------------------*/
		byte[] Load( string filename, out uint length, uint min, uint max ) {
			var fi = new System.IO.FileInfo( filename );
			if ( ( fi.Length < min ) || ( fi.Length > max ) ) { throw new Exception( "\nFile size error\n" ); }
			length = (uint)fi.Length;
			return System.IO.File.ReadAllBytes( filename );
		}

		/*----------------------------------------------------------------------------*/
		void Save( string filename, byte[] buffer, uint length ) {
			if ( buffer.Length == length ) {
				System.IO.File.WriteAllBytes( filename, buffer );
			} else {
				byte[] b = new byte[length];
				for ( int i = 0; i < length; ++i ) {
					b[i] = buffer[i];
				}
				System.IO.File.WriteAllBytes( filename, b );
			}
		}

		void BLZ_Invert( byte[] buffer, uint start, uint length ) {
			byte ch;
			uint bottom = start + length - 1;

			while ( start < bottom ) {
				ch = buffer[start];
				buffer[start++] = buffer[bottom];
				buffer[bottom--] = ch;
			}
		}

		/*----------------------------------------------------------------------------*/
		byte[] Memory( int length, int size ) {
			return new byte[length * size];
		}

		/*----------------------------------------------------------------------------*/
		public byte[] BLZ_Decode( byte[] pak_buffer ) {
			fileWasNotCompressed = false;
			byte[] raw_buffer;
			uint pak, raw, pak_end, raw_end;
			uint pak_len, raw_len, len, pos, inc_len, hdr_len, enc_len, dec_len;
			byte flags = 0, mask;

			//Console.Write( "- decoding" );

			pak_len = (uint)pak_buffer.Length;


			inc_len = BitConverter.ToUInt32( pak_buffer, (int)pak_len - 4 );
			if ( inc_len == 0 ) {
				throw new Exception( "Not coded file!" );
				enc_len = 0;
				dec_len = pak_len;
				pak_len = 0;
				raw_len = dec_len;

				fileWasNotCompressed = true;
				//Console.WriteLine();
				return pak_buffer;
			} else {
				if ( pak_len < 8 ) throw new Exception( "File has a bad header" );
				hdr_len = pak_buffer[pak_len - 5];
				if ( ( hdr_len < 0x08 ) || ( hdr_len > 0x0B ) ) throw new Exception( "Bad header length" );
				if ( pak_len <= hdr_len ) throw new Exception( "Bad length" );
				enc_len = BitConverter.ToUInt32( pak_buffer, (int)pak_len - 8 ) & 0x00FFFFFF;
				dec_len = pak_len - enc_len;
				pak_len = enc_len - hdr_len;
				raw_len = dec_len + enc_len + inc_len;
				if ( raw_len > RAW_MAXIM ) throw new Exception( "Bad decoded length" );
			}

			raw_buffer = Memory( (int)raw_len, 1 );

			pak = 0;
			raw = 0;
			pak_end = dec_len + pak_len;
			raw_end = raw_len;

			for ( len = 0; len < dec_len; len++ ) {
				raw_buffer[raw++] = pak_buffer[pak++];
			}

			BLZ_Invert( pak_buffer, dec_len, pak_len );

			//Save( "init_pak", pak_buffer, pak_len );
			//Save( "init_raw", raw_buffer, raw_len );

			mask = 0;

			while ( raw < raw_end ) {
				mask = (byte)( ( (uint)mask ) >> ( (int)BLZ_SHIFT ) );

				if ( mask == 0 ) {
					if ( pak == pak_end ) break;
					flags = pak_buffer[pak++];
					mask = BLZ_MASK;
				}

				if ( ( flags & mask ) == 0 ) {
					if ( pak == pak_end ) break;
					//Console.WriteLine( "C# Copy pak " + pak + " to raw " + raw );
					raw_buffer[raw++] = pak_buffer[pak++];
				} else {
					if ( pak + 1 >= pak_end ) break;
					pos = (uint)( pak_buffer[pak++] << 8 );
					pos |= pak_buffer[pak++];
					len = ( pos >> 12 ) + BLZ_THRESHOLD + 1;
					if ( raw + len > raw_end ) {
						Console.Write( ", WARNING: wrong decoded length!" );
						len = raw_end - raw;
					}
					pos = ( pos & 0xFFF ) + 3;
					while ( len-- != 0 ) {
						//Console.WriteLine( "C# Copy raw " + (raw - pos) + " to raw " + raw );
						raw_buffer[raw] = raw_buffer[raw - pos];
						raw++;
					}
				}
			}

			//Save( "post_pak", pak_buffer, pak_len );
			//Save( "post_raw", raw_buffer, raw_len );
			BLZ_Invert( raw_buffer, dec_len, raw_len - dec_len );
			//Save( "posti_pak", pak_buffer, pak_len );
			//Save( "posti_raw", raw_buffer, raw_len );

			raw_len = raw;

			if ( raw != raw_end ) Console.Write( ", WARNING: unexpected end of encoded file!" );

			//Save( filename + ".dec", raw_buffer, raw_len );

			//Console.WriteLine();

			return raw_buffer;
		}

		//*----------------------------------------------------------------------------
		public byte[] BLZ_Encode( byte[] raw_buffer, uint mode ) {
			byte[] pak_buffer, new_buffer;
			uint   raw_len, pak_len, new_len;

			//Console.Write("- encoding");

			raw_len = (uint)raw_buffer.Length;

			pak_buffer = null;
			pak_len = BLZ_MAXIM + 1;

			new_buffer = BLZ_Code(raw_buffer, raw_len, out new_len, mode);
			if (new_len < pak_len) {
				pak_buffer = new_buffer;
				pak_len = new_len;
			}

			//Save(filename + ".enc", pak_buffer, pak_len);

			if ( pak_buffer.Length != pak_len ) {
				byte[] retbuf = new byte[pak_len];
				for ( int i = 0; i < pak_len; ++i ) {
					retbuf[i] = pak_buffer[i];
				}
				pak_buffer = retbuf;
			}

			//Console.WriteLine();

			return pak_buffer;
		}

		private void SEARCH(ref uint l, ref uint p, ref byte[] raw_buffer, ref uint raw, ref uint raw_end, ref uint max, ref uint pos, ref uint len) {
			l = BLZ_THRESHOLD;
			
			max = raw >= BLZ_N ? BLZ_N : raw;
			for (pos = 3; pos <= max; pos++) {
				for (len = 0; len < BLZ_F; len++) {
					if (raw + len == raw_end) break;
					if (len >= pos) break;
					if (raw_buffer[raw + len] != raw_buffer[raw + len - pos]) break;
				}
				
				if (len > l) {
					p = pos;
					if ((l = len) == BLZ_F) break;
				}
			}
		}
		//*----------------------------------------------------------------------------
		byte[] BLZ_Code( byte[] raw_buffer, uint raw_len, out uint new_len, uint best ) {
			byte[] pak_buffer; 
			uint   pak, raw, raw_end, flg = 0;
			byte[] tmp;
			uint   pak_len, inc_len, hdr_len, enc_len, len = 0, pos = 0, max = 0;
			uint   len_best = 0, pos_best = 0, len_next = 0, pos_next = 0, len_post = 0, pos_post = 0;
			uint   pak_tmp, raw_tmp, raw_new;
			ushort crc;
			byte   mask;
			
			pak_tmp = 0;
			raw_tmp = raw_len;

			pak_len = raw_len + ((raw_len + 7) / 8) + 11;
			pak_buffer = Memory((int)pak_len, 1);

			raw_new = raw_len;
			if (arm9 != 0) {
				if (raw_len < 0x4000) {
					Console.Write(", WARNING: ARM9 must be greater as 16KB, switch [9] disabled");
				//} else if (
				//	BitConverter.ToUInt32(raw_buffer, 0x0) != 0xE7FFDEFFu ||
				//	BitConverter.ToUInt32(raw_buffer, 0x4) != 0xE7FFDEFFu ||
				//	BitConverter.ToUInt32(raw_buffer, 0x8) != 0xE7FFDEFFu ||
				//	BitConverter.ToUInt16(raw_buffer, 0xC) != 0xDEFFu
				//) {
				//	Console.Write(", WARNING: invalid Secure Area ID, switch [9] disabled");
				//} else if (BitConverter.ToUInt16(raw_buffer, 0x7FE) != 0) {
				//	Console.Write(", WARNING: invalid Secure Area 2KB end, switch [9] disabled");
				} else {
					crc = BLZ_CRC16(raw_buffer, 0x10, 0x07F0);
					byte[] crcbytes = BitConverter.GetBytes( crc );
					if (!(raw_buffer[0x0E] == crcbytes[0] && raw_buffer[0x0F] == crcbytes[1])) {
						Console.WriteLine("WARNING: CRC16 Secure Area 2KB do not match");
						raw_buffer[0x0E] = crcbytes[0];
						raw_buffer[0x0F] = crcbytes[1];
					}
					raw_new -= 0x4000;
				}
			}

			BLZ_Invert(raw_buffer, 0, raw_len);

			pak = 0;
			raw = 0;
			raw_end = raw_new;

			mask = 0;

			while (raw < raw_end) {
				mask = (byte)( ( (uint)mask ) >> ( (int)BLZ_SHIFT ) );

				if ( mask == 0 ) {
					flg = pak++;
					pak_buffer[flg] = 0;
					mask = BLZ_MASK;
				}

				SEARCH(ref len_best, ref pos_best, ref raw_buffer, ref raw, ref raw_end, ref max, ref pos, ref len);

				// LZ-CUE optimization start
				if (best != 0) {
					if (len_best > BLZ_THRESHOLD) {
						if (raw + len_best < raw_end) {
						raw += len_best;
						SEARCH(ref len_next, ref pos_next, ref raw_buffer, ref raw, ref raw_end, ref max, ref pos, ref len);
						raw -= len_best - 1;
						SEARCH(ref len_post, ref pos_post, ref raw_buffer, ref raw, ref raw_end, ref max, ref pos, ref len);
						raw--;

						if (len_next <= BLZ_THRESHOLD) len_next = 1;
						if (len_post <= BLZ_THRESHOLD) len_post = 1;

						if (len_best + len_next <= 1 + len_post) len_best = 1;
						}
					}
				}
				// LZ-CUE optimization end

				pak_buffer[flg] <<= 1;
				if (len_best > BLZ_THRESHOLD) {
					raw += len_best;
					pak_buffer[flg] |= 1;
					pak_buffer[pak] = (byte)(((len_best - (BLZ_THRESHOLD+1)) << 4) | ((pos_best - 3) >> 8));
					pak++;
					pak_buffer[pak] = (byte)((pos_best - 3) & 0xFF);
					pak++;
				} else {
					pak_buffer[pak] = raw_buffer[raw];
					pak++; raw++;
				}

		#if true
				if (pak + raw_len - (raw) < pak_tmp + raw_tmp) {
		#else
				if (
					(((pak - pak_buffer + raw_len - (raw - raw_buffer)) + 3) & -4)
					<
					pak_tmp + raw_tmp
				) {
		#endif
				pak_tmp = pak;
				raw_tmp = raw_len - (raw);
				}
			}

			while ((mask != 0) && (mask != 1)) {
				mask = (byte)( ( (uint)mask ) >> ( (int)BLZ_SHIFT ) );
				pak_buffer[flg] <<= 1;
			}

			pak_len = pak;

			BLZ_Invert(raw_buffer, 0, raw_len);
			BLZ_Invert(pak_buffer, 0, pak_len);

			if ((pak_tmp == 0) || (raw_len + 4 < ((pak_tmp + raw_tmp + 3) & -4) + 8)) {
				pak = 0;
				raw = 0;
				raw_end = raw_len;

				while (raw < raw_end) {
					pak_buffer[pak] = raw_buffer[raw];
					pak++; raw++;
				}

				while ((pak & 3) != 0) {
					pak_buffer[pak] = 0;
					pak++;
				}

				pak_buffer[pak] = 0;
				pak_buffer[pak+1] = 0;
				pak_buffer[pak+2] = 0;
				pak_buffer[pak+3] = 0;
				pak += 4;
			} else {
				tmp = Memory((int)(raw_tmp + pak_tmp + 11), 1);

				for (len = 0; len < raw_tmp; len++)
					tmp[len] = raw_buffer[len];

				for (len = 0; len < pak_tmp; len++)
					tmp[raw_tmp + len] = pak_buffer[len + pak_len - pak_tmp];

				pak = 0;
				// !!! uuh this isn't exactly equivalent to the C code !!!
				pak_buffer = tmp;

				//free(pak);

				pak = raw_tmp + pak_tmp;

				enc_len = pak_tmp;
				hdr_len = 8;
				inc_len = raw_len - pak_tmp - raw_tmp;

				while ((pak & 3) != 0) {
					pak_buffer[pak] = 0xFF;
					pak++;
					hdr_len++;
				}

				//*(unsigned int *)pak = enc_len + hdr_len; pak += 3;
				//*pak++ = hdr_len;
				//*(unsigned int *)pak = inc_len - hdr_len; pak += 4;
				byte[] tmpbyte = BitConverter.GetBytes( enc_len + hdr_len );
				tmpbyte.CopyTo( pak_buffer, pak );
				pak += 3;
				pak_buffer[pak] = (byte)hdr_len;
				pak++;
				tmpbyte = BitConverter.GetBytes( inc_len - hdr_len );
				tmpbyte.CopyTo( pak_buffer, pak );
				pak += 4;
			}

			//*new_len = pak - pak_buffer;
			new_len = pak;

			return(pak_buffer);
		}

		/*----------------------------------------------------------------------------*/

		//*----------------------------------------------------------------------------
		ushort BLZ_CRC16(byte[] buffer, uint bloc, uint length) {
			ushort crc;
			uint   nbits;

			crc = 0xFFFF;
			while ((length--) != 0) {
				crc ^= buffer[bloc++];
				nbits = 8;
				while ((nbits--) != 0) {
					if ( ( crc & 1 ) != 0 ) {
						crc = (ushort)(( crc >> 1 ) ^ 0xA001);
					} else {
						crc = (ushort)(crc >> 1);
					}
				}
			}

			return(crc);
		}

		/*----------------------------------------------------------------------------*/
		/*--  EOF                                           Copyright (C) 2011 CUE  --*/
		/*----------------------------------------------------------------------------*/

	}
}
