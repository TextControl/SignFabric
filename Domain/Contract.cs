using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SignFabric.Domain {
	public class Contract {
		public int Id { get; set; }
		public string ContractID { get; set; } 
		public string UserID { get; set; }
		public string Sender { get; set; }
		public string Name { get; set; }
		public DateTime Created { get; set; }
		public DateTime Sent { get; set; }
		public Signer Signer { get; set; }
		public ContractStatus Status { get; set; }
		public bool HasTrackedChanges{ get; set; }
	}

	public enum ContractStatus {
		New,
		Sent,
		Changed,
		Accepted,
		Closed
	}
}
