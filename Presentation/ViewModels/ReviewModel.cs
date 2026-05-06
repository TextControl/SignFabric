using SignFabric.Domain;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SignFabric.Presentation.ViewModels
{
    public class ReviewModel
    {
      public string EnvelopeID { get; set; }
      public bool Error { get; set; }
   }

   public class ValidateModel {
      //public string EnvelopeID { get; set; }
      public IFormFile Document { get; set; }
      public bool Error { get; set; }
      public string ErrorMessage { get; set; }
   }

   public class ThanksModel {
      public Envelope Envelope { get; set; }
      public Signer Signer { get; set; }
   }
}
