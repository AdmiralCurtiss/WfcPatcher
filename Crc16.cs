// modified from http://www.sanity-free.org/134/standard_crc_16_in_csharp.html

using System;
using System.IO;

namespace WfcPatcher {
	public class Crc16 {
		private ushort[] table = new ushort[256];

		public ushort ComputeChecksum( byte[] bytes ) {
			ushort crc = 0;
			for ( int i = 0; i < bytes.Length; ++i ) {
				byte index = (byte)( crc ^ bytes[i] );
				crc = (ushort)( ( crc >> 8 ) ^ table[index] );
			}
			return crc;
		}

		public ushort ComputeChecksum( Stream stream, int length, ushort init = 0 ) {
			ushort crc = init;
			for ( int i = 0; i < length; ++i ) {
				byte index = (byte)( ( crc ^ stream.ReadByte() ) & 0xFF );
				crc = (ushort)( ( crc >> 8 ) ^ table[index] );
			}
			return crc;
		}

		public byte[] ComputeChecksumBytes( byte[] bytes ) {
			ushort crc = ComputeChecksum( bytes );
			return BitConverter.GetBytes( crc );
		}

		public Crc16( ushort polynomial = 0xA001 ) {
			ushort value;
			ushort temp;
			for ( ushort i = 0; i < table.Length; ++i ) {
				value = 0;
				temp = i;
				for ( byte j = 0; j < 8; ++j ) {
					if ( ( ( value ^ temp ) & 0x0001 ) != 0 ) {
						value = (ushort)( ( value >> 1 ) ^ polynomial );
					} else {
						value >>= 1;
					}
					temp >>= 1;
				}
				table[i] = value;
			}
		}
	}
}
