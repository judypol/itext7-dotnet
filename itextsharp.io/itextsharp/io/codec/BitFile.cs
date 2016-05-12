/*
$Id: a6529ec5d18e186dd2589257e8e05d5519b50dc0 $

This file is part of the iText (R) project.
Copyright (c) 1998-2016 iText Group NV
Authors: Bruno Lowagie, Paulo Soares, et al.

This program is free software; you can redistribute it and/or modify
it under the terms of the GNU Affero General Public License version 3
as published by the Free Software Foundation with the addition of the
following permission added to Section 15 as permitted in Section 7(a):
FOR ANY PART OF THE COVERED WORK IN WHICH THE COPYRIGHT IS OWNED BY
ITEXT GROUP. ITEXT GROUP DISCLAIMS THE WARRANTY OF NON INFRINGEMENT
OF THIRD PARTY RIGHTS

This program is distributed in the hope that it will be useful, but
WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
or FITNESS FOR A PARTICULAR PURPOSE.
See the GNU Affero General Public License for more details.
You should have received a copy of the GNU Affero General Public License
along with this program; if not, see http://www.gnu.org/licenses or write to
the Free Software Foundation, Inc., 51 Franklin Street, Fifth Floor,
Boston, MA, 02110-1301 USA, or download the license from the following URL:
http://itextpdf.com/terms-of-use/

The interactive user interfaces in modified source and object code versions
of this program must display Appropriate Legal Notices, as required under
Section 5 of the GNU Affero General Public License.

In accordance with Section 7(b) of the GNU Affero General Public License,
a covered work must retain the producer line in every PDF that is created
or manipulated using iText.

You can be released from the requirements of the license by purchasing
a commercial license. Buying such a license is mandatory as soon as you
develop commercial activities involving the iText software without
disclosing the source code of your own applications.
These activities include: offering paid services to customers as an ASP,
serving PDFs on the fly in a web application, shipping iText with a closed
source product.

For more information, please contact iText Software Corp. at this
address: sales@itextpdf.com
*/
using System.IO;

namespace com.itextpdf.io.codec
{
	/// <summary>Came from GIFEncoder initially.</summary>
	/// <remarks>
	/// Came from GIFEncoder initially.
	/// Modified - to allow for output compressed data without the block counts
	/// which breakup the compressed data stream for GIF.
	/// </remarks>
	internal class BitFile
	{
		internal Stream output;

		internal byte[] buffer;

		internal int index;

		internal int bitsLeft;

		/// <summary>note this also indicates gif format BITFile.</summary>
		internal bool blocks = false;

		/// <param name="output">destination for output data</param>
		/// <param name="blocks">GIF LZW requires block counts for output data</param>
		public BitFile(Stream output, bool blocks)
		{
			// bits left at current index that are avail.
			this.output = output;
			this.blocks = blocks;
			buffer = new byte[256];
			index = 0;
			bitsLeft = 8;
		}

		/// <exception cref="System.IO.IOException"/>
		public virtual void Flush()
		{
			int numBytes = index + (bitsLeft == 8 ? 0 : 1);
			if (numBytes > 0)
			{
				if (blocks)
				{
					output.Write(numBytes);
				}
				output.Write(buffer, 0, numBytes);
				buffer[0] = 0;
				index = 0;
				bitsLeft = 8;
			}
		}

		/// <exception cref="System.IO.IOException"/>
		public virtual void WriteBits(int bits, int numbits)
		{
			int bitsWritten = 0;
			int numBytes = 255;
			do
			{
				// gif block count
				// This handles the GIF block count stuff
				if ((index == 254 && bitsLeft == 0) || index > 254)
				{
					if (blocks)
					{
						output.Write(numBytes);
					}
					output.Write(buffer, 0, numBytes);
					buffer[0] = 0;
					index = 0;
					bitsLeft = 8;
				}
				if (numbits <= bitsLeft)
				{
					// bits contents fit in current index byte
					if (blocks)
					{
						// GIF
						buffer[index] |= (byte)((bits & ((1 << numbits) - 1)) << (8 - bitsLeft));
						bitsWritten += numbits;
						bitsLeft -= numbits;
						numbits = 0;
					}
					else
					{
						buffer[index] |= (byte)((bits & ((1 << numbits) - 1)) << (bitsLeft - numbits));
						bitsWritten += numbits;
						bitsLeft -= numbits;
						numbits = 0;
					}
				}
				else
				{
					// bits overflow from current byte to next.
					if (blocks)
					{
						// GIF
						// if bits  > space left in current byte then the lowest order bits
						// of code are taken and put in current byte and rest put in next.
						buffer[index] |= (byte)((bits & ((1 << bitsLeft) - 1)) << (8 - bitsLeft));
						bitsWritten += bitsLeft;
						bits >>= bitsLeft;
						numbits -= bitsLeft;
						buffer[++index] = 0;
						bitsLeft = 8;
					}
					else
					{
						// if bits  > space left in current byte then the highest order bits
						// of code are taken and put in current byte and rest put in next.
						// at highest order bit location !!
						int topbits = ((int)(((uint)bits) >> (numbits - bitsLeft))) & ((1 << bitsLeft) - 
							1);
						buffer[index] |= (byte)topbits;
						numbits -= bitsLeft;
						// ok this many bits gone off the top
						bitsWritten += bitsLeft;
						buffer[++index] = 0;
						// next index
						bitsLeft = 8;
					}
				}
			}
			while (numbits != 0);
		}
	}
}