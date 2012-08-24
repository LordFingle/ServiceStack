using System;
using System.IO;
using System.Linq;
using System.Web;
using ServiceStack.Common.Web;
using ServiceStack.ServiceHost;
using ServiceStack.ServiceModel.Serialization;
using ServiceStack.Text;

namespace ServiceStack.WebHost.Endpoints.Formats
{
	public class HtmlFormat : IPlugin
	{
		public static string TitleFormat
			= @"{0} Snapshot of {1}";

		public static string HtmlTitleFormat
			= @"Snapshot of <i>{0}</i> generated by <a href=""http://www.servicestack.net"">ServiceStack</a> on <b>{1}</b>";

		private static string HtmlTemplate;

		private IAppHost AppHost { get; set; }

		public void Register(IAppHost appHost)
		{
			AppHost = appHost;
			//Register this in ServiceStack with the custom formats
			appHost.ContentTypeFilters.Register(ContentType.Html, SerializeToStream, null);
			appHost.ContentTypeFilters.Register(ContentType.JsonReport, SerializeToStream, null);

			appHost.Config.DefaultResponseContentType = ContentType.Html;
			appHost.Config.IgnoreFormatsInMetadata.Add(ContentType.Html.ToContentFormat());
			appHost.Config.IgnoreFormatsInMetadata.Add(ContentType.JsonReport.ToContentFormat());
		}

		public void SerializeToStream(IRequestContext requestContext, object dto, IHttpResponse httpRes)
		{
			if (AppHost.HtmlProviders.Any(x => x(requestContext, dto, httpRes))) return;

			var httpReq = requestContext.Get<IHttpRequest>();
			if (requestContext.ResponseContentType != ContentType.Html
				&& httpReq.ResponseContentType != ContentType.JsonReport) return;

			// Serialize then escape any potential script tags to avoid XSS when displaying as HTML
            var json = JsonDataContractSerializer.Instance.SerializeToString(dto) ?? "null";
			json = json.Replace("<", "&lt;").Replace(">", "&gt;");

			var url = httpReq.AbsoluteUri
				.Replace("format=html", "")
				.Replace("format=shtm", "")
				.TrimEnd('?', '&');

			url += url.Contains("?") ? "&" : "?";

			var now = DateTime.UtcNow;
			var requestName = httpReq.OperationName ?? dto.GetType().Name;

			string html = GetHtmlTemplate()
				.Replace("${Dto}", json)
				.Replace("${Title}", string.Format(TitleFormat, requestName, now))
				.Replace("${MvcIncludes}", MiniProfiler.Profiler.RenderIncludes().ToString())
				.Replace("${Header}", string.Format(HtmlTitleFormat, requestName, now))
				.Replace("${ServiceUrl}", url);

			var utf8Bytes = html.ToUtf8Bytes();
			httpRes.OutputStream.Write(utf8Bytes, 0, utf8Bytes.Length);
		}

		private string GetHtmlTemplate()
		{
			if (string.IsNullOrEmpty(HtmlTemplate))
			{
				HtmlTemplate = LoadHtmlTemplateFromEmbeddedResource();
			}
			return HtmlTemplate;
		}

		private string LoadHtmlTemplateFromEmbeddedResource()
		{
			// ServiceStack.WebHost.Endpoints.Formats.HtmlFormat.html
			string embeddedResourceName = GetType().Namespace + ".HtmlFormat.html";
			var stream = GetType().Assembly.GetManifestResourceStream(embeddedResourceName);
			if (stream == null)
			{
				throw new FileNotFoundException(
					"Could not load HTML template embedded resource " + embeddedResourceName,
					embeddedResourceName);
			}
			using (var streamReader = new StreamReader(stream))
			{
				return streamReader.ReadToEnd();
			}
		}
	}
}