using System;
using NUnit.Framework;
using com.itextpdf.kernel.color;
using com.itextpdf.kernel.pdf;
using com.itextpdf.kernel.pdf.canvas;
using com.itextpdf.kernel.utils;
using java.io;

namespace com.itextpdf.barcodes
{
	public class Barcode128Test
	{
		public const String sourceFolder = "./src/test/resources/com/itextpdf/barcodes/";

		public const String destinationFolder = "./target/test/com/itextpdf/barcodes/Barcode128/";

		[TestFixtureSetUp]
		public static void BeforeClass()
		{
			new File(destinationFolder).Mkdirs();
		}

		/// <exception cref="System.IO.IOException"/>
		/// <exception cref="com.itextpdf.kernel.PdfException"/>
		/// <exception cref="System.Exception"/>
		[Test]
		public virtual void Barcode01Test()
		{
			String filename = "barcode128_01.pdf";
			PdfWriter writer = new PdfWriter(destinationFolder + filename);
			PdfDocument document = new PdfDocument(writer);
			PdfPage page = document.AddNewPage();
			PdfCanvas canvas = new PdfCanvas(page);
			Barcode1D barcode = new Barcode128(document);
			barcode.SetCodeType(Barcode128.CODE128);
			barcode.SetCode("9781935182610");
			barcode.SetTextAlignment(Barcode1D.ALIGN_LEFT);
			barcode.PlaceBarcode(canvas, Color.BLACK, Color.BLACK);
			document.Close();
			NUnit.Framework.Assert.IsNull(new CompareTool().CompareByContent(destinationFolder
				 + filename, sourceFolder + "cmp_" + filename, destinationFolder, "diff_"));
		}

		/// <exception cref="System.IO.IOException"/>
		/// <exception cref="com.itextpdf.kernel.PdfException"/>
		/// <exception cref="System.Exception"/>
		[Test]
		public virtual void Barcode02Test()
		{
			String filename = "barcode128_02.pdf";
			PdfWriter writer = new PdfWriter(destinationFolder + filename);
			PdfReader reader = new PdfReader(sourceFolder + "DocumentWithTrueTypeFont1.pdf");
			PdfDocument document = new PdfDocument(reader, writer);
			PdfCanvas canvas = new PdfCanvas(document.GetLastPage());
			Barcode1D barcode = new Barcode128(document);
			barcode.SetCodeType(Barcode128.CODE128);
			barcode.SetCode("9781935182610");
			barcode.SetTextAlignment(Barcode1D.ALIGN_LEFT);
			barcode.PlaceBarcode(canvas, Color.BLACK, Color.BLACK);
			document.Close();
			NUnit.Framework.Assert.IsNull(new CompareTool().CompareByContent(destinationFolder
				 + filename, sourceFolder + "cmp_" + filename, destinationFolder, "diff_"));
		}
	}
}
