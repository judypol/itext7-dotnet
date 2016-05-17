/*
$Id$

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
using System;
using System.Collections.Generic;
using System.Text;
using iTextSharp.IO.Font.Otf;
using iTextSharp.IO.Util;
using iTextSharp.Kernel.Geom;
using iTextSharp.Layout.Element;
using iTextSharp.Layout.Layout;
using iTextSharp.Layout.Property;

namespace iTextSharp.Layout.Renderer
{
	public class LineRenderer : AbstractRenderer
	{
		protected internal float maxAscent;

		protected internal float maxDescent;

		protected internal byte[] levels;

		public override LayoutResult Layout(LayoutContext layoutContext)
		{
			Rectangle layoutBox = layoutContext.GetArea().GetBBox().Clone();
			occupiedArea = new LayoutArea(layoutContext.GetArea().GetPageNumber(), layoutBox.
				Clone().MoveDown(-layoutBox.GetHeight()).SetHeight(0));
			float curWidth = 0;
			maxAscent = 0;
			maxDescent = 0;
			int childPos = 0;
			BaseDirection baseDirection = GetProperty(iTextSharp.Layout.Property.Property.BASE_DIRECTION
				);
			foreach (IRenderer renderer in childRenderers)
			{
				if (renderer is TextRenderer)
				{
					renderer.SetParent(this);
					((TextRenderer)renderer).ApplyOtf();
					renderer.SetParent(null);
					if (baseDirection == null || baseDirection == BaseDirection.NO_BIDI)
					{
						baseDirection = renderer.GetOwnProperty(iTextSharp.Layout.Property.Property.BASE_DIRECTION
							);
					}
				}
			}
			if (levels == null && baseDirection != null && baseDirection != BaseDirection.NO_BIDI)
			{
				IList<int> unicodeIdsLst = new List<int>();
				foreach (IRenderer child in childRenderers)
				{
					if (child is TextRenderer)
					{
						GlyphLine text = ((TextRenderer)child).GetText();
						for (int i = text.start; i < text.end; i++)
						{
							System.Diagnostics.Debug.Assert(text.Get(i).GetChars().Length > 0);
							// we assume all the chars will have the same bidi group
							// we also assume pairing symbols won't get merged with other ones
							int unicode = text.Get(i).GetChars()[0];
							unicodeIdsLst.Add(unicode);
						}
					}
				}
				levels = TypographyUtils.GetBidiLevels(baseDirection, ArrayUtil.ToArray(unicodeIdsLst
					));
			}
			bool anythingPlaced = false;
			TabStop nextTabStop = null;
			LineLayoutResult result = null;
			while (childPos < childRenderers.Count)
			{
				IRenderer childRenderer = childRenderers[childPos];
				LayoutResult childResult;
				Rectangle bbox = new Rectangle(layoutBox.GetX() + curWidth, layoutBox.GetY(), layoutBox
					.GetWidth() - curWidth, layoutBox.GetHeight());
				if (childRenderer is TextRenderer)
				{
					// Delete these properties in case of relayout. We might have applied them during justify().
					childRenderer.DeleteOwnProperty(iTextSharp.Layout.Property.Property.CHARACTER_SPACING
						);
					childRenderer.DeleteOwnProperty(iTextSharp.Layout.Property.Property.WORD_SPACING);
				}
				else
				{
					if (childRenderer is TabRenderer)
					{
						if (nextTabStop != null)
						{
							IRenderer tabRenderer = childRenderers[childPos - 1];
							tabRenderer.Layout(new LayoutContext(new LayoutArea(layoutContext.GetArea().GetPageNumber
								(), bbox)));
							curWidth += tabRenderer.GetOccupiedArea().GetBBox().GetWidth();
						}
						nextTabStop = CalculateTab(childRenderer, curWidth, layoutBox.GetWidth());
						if (childPos == childRenderers.Count - 1)
						{
							nextTabStop = null;
						}
						if (nextTabStop != null)
						{
							++childPos;
							continue;
						}
					}
				}
				if (!anythingPlaced && childRenderer is TextRenderer)
				{
					((TextRenderer)childRenderer).TrimFirst();
				}
				if (nextTabStop != null && nextTabStop.GetTabAlignment() == TabAlignment.ANCHOR &&
					 childRenderer is TextRenderer)
				{
					childRenderer.SetProperty(iTextSharp.Layout.Property.Property.TAB_ANCHOR, nextTabStop
						.GetTabAnchor());
				}
				childResult = childRenderer.SetParent(this).Layout(new LayoutContext(new LayoutArea
					(layoutContext.GetArea().GetPageNumber(), bbox)));
				float childAscent = 0;
				float childDescent = 0;
				if (childRenderer is TextRenderer)
				{
					childAscent = ((TextRenderer)childRenderer).GetAscent();
					childDescent = ((TextRenderer)childRenderer).GetDescent();
				}
				else
				{
					if (childRenderer is ImageRenderer)
					{
						childAscent = childRenderer.GetOccupiedArea().GetBBox().GetHeight();
					}
				}
				maxAscent = Math.Max(maxAscent, childAscent);
				maxDescent = Math.Min(maxDescent, childDescent);
				float maxHeight = maxAscent - maxDescent;
				if (nextTabStop != null)
				{
					IRenderer tabRenderer = childRenderers[childPos - 1];
					float tabWidth = CalculateTab(layoutBox, curWidth, nextTabStop, childRenderer, childResult
						, tabRenderer);
					tabRenderer.Layout(new LayoutContext(new LayoutArea(layoutContext.GetArea().GetPageNumber
						(), bbox)));
					childResult.GetOccupiedArea().GetBBox().MoveRight(tabWidth);
					if (childResult.GetSplitRenderer() != null)
					{
						childResult.GetSplitRenderer().GetOccupiedArea().GetBBox().MoveRight(tabWidth);
					}
					nextTabStop = null;
					curWidth += tabWidth;
				}
				curWidth += childResult.GetOccupiedArea().GetBBox().GetWidth();
				occupiedArea.SetBBox(new Rectangle(layoutBox.GetX(), layoutBox.GetY() + layoutBox
					.GetHeight() - maxHeight, curWidth, maxHeight));
				if (childResult.GetStatus() != LayoutResult.FULL)
				{
					LineRenderer[] split = Split();
					split[0].childRenderers = new List<IRenderer>(childRenderers.SubList(0, childPos)
						);
					bool wordWasSplitAndItWillFitOntoNextLine = false;
					if (childResult is TextLayoutResult && ((TextLayoutResult)childResult).IsWordHasBeenSplit
						())
					{
						LayoutResult newLayoutResult = childRenderer.Layout(layoutContext);
						if (newLayoutResult is TextLayoutResult && !((TextLayoutResult)newLayoutResult).IsWordHasBeenSplit
							())
						{
							wordWasSplitAndItWillFitOntoNextLine = true;
						}
					}
					if (wordWasSplitAndItWillFitOntoNextLine)
					{
						split[1].childRenderers.Add(childRenderer);
						split[1].childRenderers.AddAll(childRenderers.SubList(childPos + 1, childRenderers
							.Count));
					}
					else
					{
						if (childResult.GetStatus() == LayoutResult.PARTIAL)
						{
							split[0].AddChild(childResult.GetSplitRenderer());
							anythingPlaced = true;
						}
						if (childResult.GetStatus() == LayoutResult.PARTIAL && childResult.GetOverflowRenderer
							() is ImageRenderer)
						{
							((ImageRenderer)childResult.GetOverflowRenderer()).AutoScale(layoutContext.GetArea
								());
						}
						if (null != childResult.GetOverflowRenderer())
						{
							split[1].childRenderers.Add(childResult.GetOverflowRenderer());
						}
						split[1].childRenderers.AddAll(childRenderers.SubList(childPos + 1, childRenderers
							.Count));
						// no sense to process empty renderer
						if (split[1].childRenderers.Count == 0)
						{
							split[1] = null;
						}
					}
					result = new LineLayoutResult(anythingPlaced ? LayoutResult.PARTIAL : LayoutResult
						.NOTHING, occupiedArea, split[0], split[1]);
					if (childResult.GetStatus() == LayoutResult.PARTIAL && childResult is TextLayoutResult
						 && ((TextLayoutResult)childResult).IsSplitForcedByNewline())
					{
						result.SetSplitForcedByNewline(true);
					}
					break;
				}
				else
				{
					anythingPlaced = true;
					childPos++;
				}
			}
			if (result == null)
			{
				if (anythingPlaced)
				{
					result = new LineLayoutResult(LayoutResult.FULL, occupiedArea, null, null);
				}
				else
				{
					result = new LineLayoutResult(LayoutResult.NOTHING, occupiedArea, null, this);
				}
			}
			// Consider for now that all the children have the same font, and that after reordering text pieces
			// can be reordered, but cannot be split.
			if (baseDirection != null && baseDirection != BaseDirection.NO_BIDI)
			{
				IList<IRenderer> children = null;
				if (result.GetStatus() == LayoutResult.PARTIAL)
				{
					children = result.GetSplitRenderer().GetChildRenderers();
				}
				else
				{
					if (result.GetStatus() == LayoutResult.FULL)
					{
						children = GetChildRenderers();
					}
				}
				if (children != null)
				{
					IList<LineRenderer.RendererGlyph> lineGlyphs = new List<LineRenderer.RendererGlyph
						>();
					foreach (IRenderer child in children)
					{
						if (child is TextRenderer)
						{
							GlyphLine childLine = ((TextRenderer)child).line;
							for (int i = childLine.start; i < childLine.end; i++)
							{
								lineGlyphs.Add(new LineRenderer.RendererGlyph(childLine.Get(i), (TextRenderer)child
									));
							}
						}
					}
					byte[] lineLevels = new byte[lineGlyphs.Count];
					if (levels != null)
					{
						System.Array.Copy(levels, 0, lineLevels, 0, lineGlyphs.Count);
					}
					int[] reorder = TypographyUtils.ReorderLine(lineGlyphs, lineLevels, levels);
					if (reorder != null)
					{
						children.Clear();
						int pos = 0;
						IList<int[]> reversedRanges = new List<int[]>();
						int initialPos = 0;
						bool reversed = false;
						int offset = 0;
						while (pos < lineGlyphs.Count)
						{
							IRenderer renderer_1 = lineGlyphs[pos].renderer;
							TextRenderer newRenderer = new TextRenderer((TextRenderer)renderer_1);
							newRenderer.DeleteOwnProperty(iTextSharp.Layout.Property.Property.REVERSED);
							children.Add(newRenderer);
							((TextRenderer)children[children.Count - 1]).line = new GlyphLine(((TextRenderer)
								children[children.Count - 1]).line);
							GlyphLine gl = ((TextRenderer)children[children.Count - 1]).line;
							IList<Glyph> replacementGlyphs = new List<Glyph>();
							while (pos < lineGlyphs.Count && lineGlyphs[pos].renderer == renderer_1)
							{
								if (pos < lineGlyphs.Count - 1)
								{
									if (reorder[pos] == reorder[pos + 1] + 1)
									{
										reversed = true;
									}
									else
									{
										if (reversed)
										{
											IList<int[]> reversedRange = new List<int[]>();
											reversedRange.Add(new int[] { initialPos - offset, pos - offset });
											newRenderer.SetProperty(iTextSharp.Layout.Property.Property.REVERSED, reversedRange
												);
											reversedRanges.Add(new int[] { initialPos - offset, pos - offset });
											reversed = false;
										}
										initialPos = pos + 1;
									}
								}
								replacementGlyphs.Add(lineGlyphs[pos].glyph);
								pos++;
							}
							if (reversed)
							{
								IList<int[]> reversedRange = new List<int[]>();
								reversedRange.Add(new int[] { initialPos - offset, pos - 1 - offset });
								newRenderer.SetProperty(iTextSharp.Layout.Property.Property.REVERSED, reversedRange
									);
								reversedRanges.Add(new int[] { initialPos - offset, pos - 1 - offset });
								reversed = false;
								initialPos = pos;
							}
							offset = initialPos;
							gl.SetGlyphs(replacementGlyphs);
						}
						if (reversed)
						{
							if (children.Count == 1)
							{
								offset = 0;
							}
							IList<int[]> reversedRange = new List<int[]>();
							reversedRange.Add(new int[] { initialPos - offset, pos - offset - 1 });
							lineGlyphs[pos - 1].renderer.SetProperty(iTextSharp.Layout.Property.Property.REVERSED
								, reversedRange);
							reversedRanges.Add(new int[] { initialPos - offset, pos - 1 - offset });
						}
						if (!reversedRanges.IsEmpty())
						{
							if (children.Count == 1)
							{
								lineGlyphs[0].renderer.SetProperty(iTextSharp.Layout.Property.Property.REVERSED, 
									reversedRanges);
							}
						}
						float currentXPos = layoutContext.GetArea().GetBBox().GetLeft();
						foreach (IRenderer child_1 in children)
						{
							float currentWidth = ((TextRenderer)child_1).CalculateLineWidth();
							((TextRenderer)child_1).occupiedArea.GetBBox().SetX(currentXPos).SetWidth(currentWidth
								);
							currentXPos += currentWidth;
						}
					}
					if (result.GetStatus() == LayoutResult.PARTIAL)
					{
						LineRenderer overflow = (LineRenderer)result.GetOverflowRenderer();
						if (levels != null)
						{
							overflow.levels = new byte[levels.Length - lineLevels.Length];
							System.Array.Copy(levels, lineLevels.Length, overflow.levels, 0, overflow.levels.
								Length);
						}
					}
				}
			}
			if (anythingPlaced)
			{
				LineRenderer processed = result.GetStatus() == LayoutResult.FULL ? this : (LineRenderer
					)result.GetSplitRenderer();
				processed.AdjustChildrenYLine().TrimLast();
			}
			return result;
		}

		public virtual float GetMaxAscent()
		{
			return maxAscent;
		}

		public virtual float GetMaxDescent()
		{
			return maxDescent;
		}

		public virtual float GetYLine()
		{
			return occupiedArea.GetBBox().GetY() - maxDescent;
		}

		public virtual float GetLeadingValue(Leading leading)
		{
			switch (leading.GetType())
			{
				case Leading.FIXED:
				{
					return leading.GetValue();
				}

				case Leading.MULTIPLIED:
				{
					return occupiedArea.GetBBox().GetHeight() * leading.GetValue();
				}

				default:
				{
					throw new InvalidOperationException();
				}
			}
		}

		public override IRenderer GetNextRenderer()
		{
			return new LineRenderer();
		}

		protected internal override float GetFirstYLineRecursively()
		{
			return GetYLine();
		}

		protected internal virtual void Justify(float width)
		{
			float ratio = GetPropertyAsFloat(iTextSharp.Layout.Property.Property.SPACING_RATIO
				);
			float freeWidth = occupiedArea.GetBBox().GetX() + width - GetLastChildRenderer().
				GetOccupiedArea().GetBBox().GetX() - GetLastChildRenderer().GetOccupiedArea().GetBBox
				().GetWidth();
			int numberOfSpaces = GetNumberOfSpaces();
			int baseCharsCount = BaseCharactersCount();
			float baseFactor = freeWidth / (ratio * numberOfSpaces + (1 - ratio) * (baseCharsCount
				 - 1));
			float wordSpacing = ratio * baseFactor;
			float characterSpacing = (1 - ratio) * baseFactor;
			float lastRightPos = occupiedArea.GetBBox().GetX();
			for (IEnumerator<IRenderer> iterator = childRenderers.GetEnumerator(); iterator.MoveNext
				(); )
			{
				IRenderer child = iterator.Current;
				float childX = child.GetOccupiedArea().GetBBox().GetX();
				child.Move(lastRightPos - childX, 0);
				childX = lastRightPos;
				if (child is TextRenderer)
				{
					float childHSCale = ((TextRenderer)child).GetPropertyAsFloat(iTextSharp.Layout.Property.Property
						.HORIZONTAL_SCALING, 1f);
					child.SetProperty(iTextSharp.Layout.Property.Property.CHARACTER_SPACING, characterSpacing
						 / childHSCale);
					child.SetProperty(iTextSharp.Layout.Property.Property.WORD_SPACING, wordSpacing /
						 childHSCale);
					bool isLastTextRenderer = !iterator.MoveNext();
					float widthAddition = (isLastTextRenderer ? (((TextRenderer)child).LineLength() -
						 1) : ((TextRenderer)child).LineLength()) * characterSpacing + wordSpacing * ((TextRenderer
						)child).GetNumberOfSpaces();
					child.GetOccupiedArea().GetBBox().SetWidth(child.GetOccupiedArea().GetBBox().GetWidth
						() + widthAddition);
				}
				lastRightPos = childX + child.GetOccupiedArea().GetBBox().GetWidth();
			}
			GetOccupiedArea().GetBBox().SetWidth(width);
		}

		protected internal virtual int GetNumberOfSpaces()
		{
			int spaces = 0;
			foreach (IRenderer child in childRenderers)
			{
				if (child is TextRenderer)
				{
					spaces += ((TextRenderer)child).GetNumberOfSpaces();
				}
			}
			return spaces;
		}

		/// <summary>Gets the total lengths of characters in this line.</summary>
		/// <remarks>
		/// Gets the total lengths of characters in this line. Other elements (images, tables) are not taken
		/// into account.
		/// </remarks>
		protected internal virtual int Length()
		{
			int length = 0;
			foreach (IRenderer child in childRenderers)
			{
				if (child is TextRenderer)
				{
					length += ((TextRenderer)child).LineLength();
				}
			}
			return length;
		}

		/// <summary>Returns the number of base characters, i.e.</summary>
		/// <remarks>Returns the number of base characters, i.e. non-mark characters</remarks>
		protected internal virtual int BaseCharactersCount()
		{
			int count = 0;
			foreach (IRenderer child in childRenderers)
			{
				if (child is TextRenderer)
				{
					count += ((TextRenderer)child).BaseCharactersCount();
				}
			}
			return count;
		}

		public override String ToString()
		{
			StringBuilder sb = new StringBuilder();
			foreach (IRenderer renderer in childRenderers)
			{
				sb.Append(renderer.ToString());
			}
			return sb.ToString();
		}

		protected internal virtual LineRenderer CreateSplitRenderer()
		{
			return (LineRenderer)GetNextRenderer();
		}

		protected internal virtual LineRenderer CreateOverflowRenderer()
		{
			return (LineRenderer)GetNextRenderer();
		}

		protected internal virtual LineRenderer[] Split()
		{
			LineRenderer splitRenderer = CreateSplitRenderer();
			splitRenderer.occupiedArea = occupiedArea.Clone();
			splitRenderer.parent = parent;
			splitRenderer.maxAscent = maxAscent;
			splitRenderer.maxDescent = maxDescent;
			splitRenderer.levels = levels;
			splitRenderer.AddAllProperties(GetOwnProperties());
			LineRenderer overflowRenderer = CreateOverflowRenderer();
			overflowRenderer.parent = parent;
			overflowRenderer.levels = levels;
			overflowRenderer.AddAllProperties(GetOwnProperties());
			return new LineRenderer[] { splitRenderer, overflowRenderer };
		}

		protected internal virtual LineRenderer AdjustChildrenYLine()
		{
			float actualYLine = occupiedArea.GetBBox().GetY() + occupiedArea.GetBBox().GetHeight
				() - maxAscent;
			foreach (IRenderer renderer in childRenderers)
			{
				if (renderer is TextRenderer)
				{
					((TextRenderer)renderer).MoveYLineTo(actualYLine);
				}
				else
				{
					if (renderer is ImageRenderer)
					{
						renderer.GetOccupiedArea().GetBBox().SetY(occupiedArea.GetBBox().GetY() - maxDescent
							);
					}
					else
					{
						renderer.GetOccupiedArea().GetBBox().SetY(occupiedArea.GetBBox().GetY());
					}
				}
			}
			return this;
		}

		protected internal virtual LineRenderer TrimLast()
		{
			IRenderer lastRenderer = childRenderers.Count > 0 ? childRenderers[childRenderers
				.Count - 1] : null;
			if (lastRenderer is TextRenderer)
			{
				float trimmedSpace = ((TextRenderer)lastRenderer).TrimLast();
				occupiedArea.GetBBox().SetWidth(occupiedArea.GetBBox().GetWidth() - trimmedSpace);
			}
			return this;
		}

		protected internal virtual bool ContainsImage()
		{
			foreach (IRenderer renderer in childRenderers)
			{
				if (renderer is ImageRenderer)
				{
					return true;
				}
			}
			return false;
		}

		private IRenderer GetLastChildRenderer()
		{
			return childRenderers[childRenderers.Count - 1];
		}

		private TabStop GetNextTabStop(float curWidth)
		{
			NavigableMap<float, TabStop> tabStops = GetProperty(iTextSharp.Layout.Property.Property
				.TAB_STOPS);
			KeyValuePair<float, TabStop> nextTabStopEntry = null;
			TabStop nextTabStop = null;
			if (tabStops != null)
			{
				nextTabStopEntry = tabStops.HigherEntry(curWidth);
			}
			if (nextTabStopEntry != null)
			{
				nextTabStop = nextTabStopEntry.Value;
			}
			return nextTabStop;
		}

		/// <summary>Calculates and sets encountered tab size.</summary>
		/// <remarks>
		/// Calculates and sets encountered tab size.
		/// Returns null, if processing is finished and layout can be performed for the tab renderer;
		/// otherwise, in case when the tab should be processed after the next element in the line, this method returns corresponding tab stop.
		/// </remarks>
		private TabStop CalculateTab(IRenderer childRenderer, float curWidth, float lineWidth
			)
		{
			TabStop nextTabStop = GetNextTabStop(curWidth);
			if (nextTabStop == null)
			{
				ProcessDefaultTab(childRenderer, curWidth, lineWidth);
				return null;
			}
			childRenderer.SetProperty(iTextSharp.Layout.Property.Property.TAB_LEADER, nextTabStop
				.GetTabLeader());
			childRenderer.SetProperty(iTextSharp.Layout.Property.Property.WIDTH, UnitValue.CreatePointValue
				(nextTabStop.GetTabPosition() - curWidth));
			childRenderer.SetProperty(iTextSharp.Layout.Property.Property.HEIGHT, maxAscent -
				 maxDescent);
			if (nextTabStop.GetTabAlignment() == TabAlignment.LEFT)
			{
				return null;
			}
			return nextTabStop;
		}

		/// <summary>Calculates and sets tab size with the account of the element that is next in the line after the tab.
		/// 	</summary>
		/// <remarks>
		/// Calculates and sets tab size with the account of the element that is next in the line after the tab.
		/// Returns resulting width of the tab.
		/// </remarks>
		private float CalculateTab(Rectangle layoutBox, float curWidth, TabStop tabStop, 
			IRenderer nextElementRenderer, LayoutResult nextElementResult, IRenderer tabRenderer
			)
		{
			float childWidth = 0;
			if (nextElementRenderer != null)
			{
				childWidth = nextElementRenderer.GetOccupiedArea().GetBBox().GetWidth();
			}
			float tabWidth = 0;
			switch (tabStop.GetTabAlignment())
			{
				case TabAlignment.RIGHT:
				{
					tabWidth = tabStop.GetTabPosition() - curWidth - childWidth;
					break;
				}

				case TabAlignment.CENTER:
				{
					tabWidth = tabStop.GetTabPosition() - curWidth - childWidth / 2;
					break;
				}

				case TabAlignment.ANCHOR:
				{
					float anchorPosition = -1;
					if (nextElementRenderer is TextRenderer)
					{
						anchorPosition = ((TextRenderer)nextElementRenderer).GetTabAnchorCharacterPosition
							();
					}
					if (anchorPosition == -1)
					{
						anchorPosition = childWidth;
					}
					tabWidth = tabStop.GetTabPosition() - curWidth - anchorPosition;
					break;
				}
			}
			if (tabWidth < 0)
			{
				tabWidth = 0;
			}
			if (curWidth + tabWidth + childWidth > layoutBox.GetWidth())
			{
				tabWidth -= (curWidth + childWidth + tabWidth) - layoutBox.GetWidth();
			}
			tabRenderer.SetProperty(iTextSharp.Layout.Property.Property.WIDTH, UnitValue.CreatePointValue
				(tabWidth));
			tabRenderer.SetProperty(iTextSharp.Layout.Property.Property.HEIGHT, maxAscent - maxDescent
				);
			return tabWidth;
		}

		private void ProcessDefaultTab(IRenderer tabRenderer, float curWidth, float lineWidth
			)
		{
			float tabDefault = GetPropertyAsFloat(iTextSharp.Layout.Property.Property.TAB_DEFAULT
				);
			float tabWidth = tabDefault - curWidth % tabDefault;
			if (curWidth + tabWidth > lineWidth)
			{
				tabWidth = lineWidth - curWidth;
			}
			tabRenderer.SetProperty(iTextSharp.Layout.Property.Property.WIDTH, UnitValue.CreatePointValue
				(tabWidth));
			tabRenderer.SetProperty(iTextSharp.Layout.Property.Property.HEIGHT, maxAscent - maxDescent
				);
		}

		internal class RendererGlyph
		{
			public RendererGlyph(Glyph glyph, TextRenderer textRenderer)
			{
				this.glyph = glyph;
				this.renderer = textRenderer;
			}

			public Glyph glyph;

			public TextRenderer renderer;
		}
	}
}