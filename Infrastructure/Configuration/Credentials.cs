using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SignFabric.Infrastructure.Configuration {
	public class Credentials {
      public DSServer DSServer { get; set; }
      public Email Email { get; set; }
   }

   public class DSServer {
      public string ClientId { get; set; }
      public string ClientSecret { get; set; }
      public string ServiceUrl { get; set; }
   }

   public class Email {
      public string Username { get; set; }
      public string Password { get; set; }
      public bool PasswordProtected { get; set; }
      public string From { get; set; }
      public string Bcc { get; set; }
      public string Server { get; set; }
      public int Port { get; set; }
   }
}


