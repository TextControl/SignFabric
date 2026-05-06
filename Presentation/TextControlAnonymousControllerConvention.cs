using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace SignFabric.Presentation {
	public class TextControlAnonymousControllerConvention : IControllerModelConvention {
		public void Apply(ControllerModel controller) {
			if (!IsTextControlController(controller)) {
				return;
			}

			var allowAnonymous = new AllowAnonymousAttribute();

			foreach (var selector in controller.Selectors) {
				selector.EndpointMetadata.Add(allowAnonymous);
			}

			foreach (var action in controller.Actions) {
				foreach (var selector in action.Selectors) {
					selector.EndpointMetadata.Add(allowAnonymous);
				}
			}
		}

		private static bool IsTextControlController(ControllerModel controller) {
			return controller.ControllerName == "TextControl" ||
				controller.ControllerType.FullName?.Contains("TXTextControl", System.StringComparison.OrdinalIgnoreCase) == true;
		}
	}
}
