using System.Net;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Totoro.Core.Agent;

namespace Totoro.Agent;

/// <summary>
/// The agent tools over HTTP on localhost, for coding agents and scripts to call without driving the Agent tools page:
/// <c>GET /tools</c> lists them (name, description, schema), <c>POST /tools/{name}</c> with JSON arguments calls one and
/// returns its JSON result. Debug builds only: it calls tools as the page does, without the chat's approval.
/// </summary>
public static class AgentToolServer
{
	public const int Port = 47700;

	private static readonly JsonSerializerOptions s_json = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

	public static void Start(IAgentTools agentTools, ILogger logger)
	{
		// "localhost" needs no URL reservation, and http.sys turns away any other Host header (DNS rebinding).
		var listener = new HttpListener { Prefixes = { $"http://localhost:{Port}/" } };
		try
		{
			listener.Start();
		}
		catch (HttpListenerException ex)
		{
			logger.LogWarning(ex, "Couldn't serve the agent tools on port {Port}", Port);
			return;
		}

		logger.LogInformation("Agent tools served at http://localhost:{Port}/tools", Port);
		_ = Task.Run(async () =>
		{
			while (listener.IsListening)
			{
				var context = await listener.GetContextAsync();
				_ = HandleAsync(context, agentTools, logger);
			}
		});
	}

	private static async Task HandleAsync(HttpListenerContext context, IAgentTools agentTools, ILogger logger)
	{
		var request = context.Request;
		var response = context.Response;
		try
		{
			// A web page's request carries an Origin: only local tools (curl, scripts) may call.
			if (request.Headers["Origin"] is not null)
			{
				await WriteAsync(response, HttpStatusCode.Forbidden, new AgentToolError("Requests from web pages aren't allowed."));
				return;
			}

			var path = request.Url!.AbsolutePath.TrimEnd('/');
			if (request.HttpMethod == "GET" && path == "/tools")
			{
				await WriteAsync(response, HttpStatusCode.OK, agentTools.Tools.OrderBy(x => x.Name)
					.Select(x => new { name = x.Name, description = x.Description, schema = x.JsonSchema }));
				return;
			}

			if (request.HttpMethod != "POST" || !path.StartsWith("/tools/", StringComparison.Ordinal))
			{
				await WriteAsync(response, HttpStatusCode.NotFound, new AgentToolError("GET /tools, or POST /tools/{name} with JSON arguments."));
				return;
			}

			var name = path["/tools/".Length..];
			if (agentTools.Tools.FirstOrDefault(x => x.Name == name) is not { } tool)
			{
				await WriteAsync(response, HttpStatusCode.NotFound, new AgentToolError($"No tool named {name}: GET /tools lists them."));
				return;
			}

			Dictionary<string, object?> arguments;
			try
			{
				using var reader = new StreamReader(request.InputStream);
				var body = await reader.ReadToEndAsync();
				arguments = JsonSerializer.Deserialize<Dictionary<string, object?>>(string.IsNullOrWhiteSpace(body) ? "{}" : body) ?? [];
			}
			catch (JsonException ex)
			{
				await WriteAsync(response, HttpStatusCode.BadRequest, new AgentToolError($"The arguments aren't valid JSON: {ex.Message}"));
				return;
			}

			// Runs on the UI thread, and turns its own failures into an AgentToolError result (see AgentTools).
			await WriteAsync(response, HttpStatusCode.OK, await tool.InvokeAsync(new AIFunctionArguments(arguments)));
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Agent tool request {Url} failed", request.Url);
			try
			{
				await WriteAsync(response, HttpStatusCode.InternalServerError, new AgentToolError(ex.Message));
			}
			catch
			{
				// The client went away.
			}
		}
	}

	private static async Task WriteAsync(HttpListenerResponse response, HttpStatusCode status, object? value)
	{
		response.StatusCode = (int)status;
		response.ContentType = "application/json; charset=utf-8";
		await response.OutputStream.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(value, s_json));
		response.Close();
	}
}
