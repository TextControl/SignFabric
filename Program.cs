using SignFabric.Application;
using SignFabric.Infrastructure.Configuration;
using SignFabric.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;

namespace SignFabric {
	public class Program {
		public static void Main(string[] args) {
			var builder = WebApplication.CreateBuilder(args);

			builder.Services
				.AddPresentation(builder.Configuration)
				.AddInfrastructureServices(builder.Configuration)
				.AddApplicationServices();

			var app = builder.Build();

			ConfigureDataDirectories(app.Environment.ContentRootPath, builder.Configuration.GetSection("AppSettings").Get<AppSettings>());

			app.UsePresentationPipeline();
			app.Run();
		}

		private static void ConfigureDataDirectories(string contentRootPath, AppSettings settings) {
			var dataDirectory = ResolvePath(contentRootPath, settings?.DataDirectory, "App_Data");
			var liteDbDirectory = ResolvePath(contentRootPath, settings?.DatabaseDirectory, "Data");

			Directory.CreateDirectory(dataDirectory);
			Directory.CreateDirectory(liteDbDirectory);

			AppDomain.CurrentDomain.SetData("DataDirectory", dataDirectory);
		}

		private static string ResolvePath(string contentRootPath, string configuredPath, string fallbackPath) {
			var path = string.IsNullOrWhiteSpace(configuredPath)
				? fallbackPath
				: configuredPath;

			return Path.IsPathRooted(path)
				? Path.GetFullPath(path)
				: Path.GetFullPath(Path.Combine(contentRootPath, path));
		}
	}
}
