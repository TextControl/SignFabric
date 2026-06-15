using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SignFabric.Domain {

	public class SignatureModel {
		public string Document { get; set; }
		public string DocumentHashSha256 { get; set; }
		public string SignatureImageHashSha256 { get; set; }
		public int NumPages { get; set; }
		public string SignerInitials { get; set; }
		public string SignerName { get; set; }
		public DateTime TimeStamp { get; set; }
		public string UniqueId { get; set; }
		public string IPAddress { get; set; }
		public string UserAgent { get; set; }
		public string SignatureBoxName { get; set; }
		public string SignatureMethod { get; set; }
		public List<SignatureStroke> SignatureLines { get; set; } = new List<SignatureStroke>();

		public int SignatureLineCount => SignatureLines?.Count ?? 0;
		public int SignaturePointCount => SignatureLines?.Sum(line => line.Points?.Count ?? 0) ?? 0;
	}

	public class SignatureStroke {
		public int Index { get; set; }
		public List<SignaturePointModel> Points { get; set; } = new List<SignaturePointModel>();
	}

	public class SignaturePointModel {
		public int X { get; set; }
		public int Y { get; set; }
		public long CreationTimeStamp { get; set; }
		public DateTime? CreatedAtUtc { get; set; }
	}
}
