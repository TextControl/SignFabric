using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TXTextControl;

namespace SignFabric.Infrastructure.Services.TextControl {
	public static class TextControlExtensions {
		public static int Load(this ServerTextControl serverTextControl, LoadSettings ls, byte[] data, int iterator = 5) {
			try {

				// check format based on first 4 bytes
				if (iterator == 5) {
					var test = data.Take(4);

					foreach (var hintFormat in HintFormats) {
						if (test.SequenceEqual(hintFormat.Value)) {
							iterator = hintFormat.Key;
							break;
						}
					}
				}

				switch (iterator) {
					case 0:
						serverTextControl.Load(data, BinaryStreamType.WordprocessingML, ls);
						return 1024;

					case 1:
						serverTextControl.Load(data, BinaryStreamType.MSWord, ls);
						return 64;

					case 2:
						serverTextControl.Load(data, BinaryStreamType.AdobePDF, ls);
						return 512;

					case 3:
						serverTextControl.Load(Encoding.UTF8.GetString(data), StringStreamType.RichTextFormat, ls);
						return 8;

					case 4:
						serverTextControl.Load(Encoding.UTF8.GetString(data), StringStreamType.HTMLFormat, ls);
						return 4;

					case 5:
						serverTextControl.Load(data, BinaryStreamType.InternalUnicodeFormat, ls);
						return 32;

					case 6:
						serverTextControl.Load(data, BinaryStreamType.SpreadsheetML, ls);
						return 4096;
				}
			}
			catch (MergeBlockConversionException) { }
			catch {
				if (iterator != 6) {
					iterator++;
					return Load(serverTextControl, ls, data, iterator);
				}
			}

			return 0;
		}

		private static readonly Dictionary<int, byte[]> HintFormats = new Dictionary<int, byte[]> {
			[0] = new byte[] { 80, 75, 3, 4 },			// WordProcessingML
			[1] = new byte[] { 208, 207, 17, 224 },	// MSWord
			[2] = new byte[] { 37, 80, 68, 70 },		// AdobePDF
			[3] = new byte[] { 123, 92, 114, 116 },	// RichTextFormat
			[5] = new byte[] { 8, 7, 1, 0 }				// InternalUnicodeFormat
		};
	}
}
