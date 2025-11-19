// Copyright (c) Microsoft. All rights reserved.

// This sample shows how to create and use a simple AI agent with OpenAI as the backend.

using System.ClientModel;
using Microsoft.Agents.AI;
using OpenAI;
using OpenAI.Chat;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using NewRelic.Api.Agent;

var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? throw new InvalidOperationException("OPENAI_API_KEY is not set.");
var model = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-4o-mini";
string vendor = "OpenAI";
string role = "Joker";
string instructions = "You are good at telling jokes.";
string content = "Tell me a joke about a pirate.";

AIAgent agent = new OpenAIClient(apiKey)
    .GetChatClient(model)
    .CreateAIAgent(instructions: instructions, name: role);

UserChatMessage chatMessage = new(content);

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/joke", JokeFunction);
app.MapGet("/jokeStreaming", JokeStreamingFunction);

app.Run();

async Task<string> JokeFunction()
{
    ChatCompletion chatCompletion = await agent.RunAsync([chatMessage]);
    Console.WriteLine(chatCompletion.Content.Last().Text);

    IAgent nrAgent = NewRelic.Api.Agent.NewRelic.GetAgent();
    ITraceMetadata traceMetadata = nrAgent.TraceMetadata;
    string traceId = traceMetadata.TraceId;
    string spanId = traceMetadata.SpanId;
    bool isSampled = traceMetadata.IsSampled;
    var completionId = Guid.NewGuid().ToString();

    var tokenIn = chatCompletion.Usage.InputTokenCount;
    var tokenOut = chatCompletion.Usage.OutputTokenCount;
    var tokenMax = tokenIn + tokenOut;

    var attributes = new Dictionary<string, object>
            {
                { "id", completionId },
                { "request_id", chatCompletion.Id },
                { "span_id", spanId },
                { "trace_id", traceId },
                { "request.model", model },
                { "response.model", chatCompletion.Model },
                { "token_count", tokenMax },
                { "request.max_tokens", tokenMax },
                { "response.number_of_messages", 2 },
                { "response.choices.finish_reason", chatCompletion.FinishReason.ToString() },
                { "vendor", vendor },
                { "ingest_source", "DotNet" },
                { "tags.aiEnabledApp", true },
                // { "duration", (float)segment.DurationOrZero.TotalMilliseconds },
                //{ "llm.<user_defined_metadata>", "Pulled from Transaction metadata in RecordLlmEvent" },
                //{ "response.headers.<vendor_specific_headers>", "See LLM headers below" },
            };

    // if (isError)
    // {
    //     attributes.Add("error", isError);
    // }

    // if (temperature.HasValue)
    // {
    //     attributes.Add("request.temperature", temperature);
    // }
    // if (maxTokens.HasValue)
    // {
    //     attributes.Add("request.max_tokens", maxTokens);
    // }

    // // LLM Metadata
    // if (headers != null)
    // {
    //     AddHeaderAttributes(headers, attributes);
    // }

    // if (!string.IsNullOrEmpty(organization))
    // {
    //     attributes.Add("response.organization", organization);
    // }

    NewRelic.Api.Agent.NewRelic.RecordCustomEvent("LlmChatCompletionSummary", attributes);

    var attributesMsgIn = new Dictionary<string, object>
            {
                { "id", completionId }, //string.IsNullOrEmpty(responseId) ? Guid.NewGuid().ToString() : (responseId + "-" + sequence) },
                { "request_id", chatCompletion.Id },
                { "span_id", spanId },
                { "trace_id", traceId },
                { "response.model", chatCompletion.Model },
                //{ "token_count", tokenIn },
                { "vendor", vendor },
                { "ingest_source", "DotNet" },
                { "content", content },
                { "role", "user" },
                { "sequence", 0 },
                { "is_response", false },
                { "completion_id", completionId },
                { "tags.aiEnabledApp", true },
                //{ "llm.<user_defined_metadata>", "Pulled from Transaction metadata in RecordLlmEvent" },
            };

    NewRelic.Api.Agent.NewRelic.RecordCustomEvent("LlmChatCompletionMessage", attributesMsgIn);

    var attributesMsgOut = new Dictionary<string, object>
            {
                { "id", completionId }, //string.IsNullOrEmpty(responseId) ? Guid.NewGuid().ToString() : (responseId + "-" + sequence) },
                { "request_id", chatCompletion.Id },
                { "span_id", spanId },
                { "trace_id", traceId },
                { "response.model", chatCompletion.Model },
                //{ "token_count", tokenIn },
                { "vendor", vendor },
                { "ingest_source", "DotNet" },
                { "content", chatCompletion.Content.Last().Text },
                { "role", "assistant" },
                { "sequence", 1 },
                { "is_response", true },
                { "completion_id", completionId },
                { "tags.aiEnabledApp", true },
                //{ "llm.<user_defined_metadata>", "Pulled from Transaction metadata in RecordLlmEvent" },
            };

    NewRelic.Api.Agent.NewRelic.RecordCustomEvent("LlmChatCompletionMessage", attributesMsgOut);

    return chatCompletion.Content.Last().Text;
}

async Task<string> JokeStreamingFunction()
{
    AsyncCollectionResult<StreamingChatCompletionUpdate> completionUpdates = agent.RunStreamingAsync([chatMessage]);
    await foreach (StreamingChatCompletionUpdate completionUpdate in completionUpdates)
    {
        if (completionUpdate.ContentUpdate.Count > 0)
        {
            Console.WriteLine(completionUpdate.ContentUpdate[0].Text);
        }
    }
    return "Streaming completed.";
}
// // Invoke the agent and output the text result.
// ChatCompletion chatCompletion = await agent.RunAsync([chatMessage]);
// Console.WriteLine(chatCompletion.Content.Last().Text);

// // Invoke the agent with streaming support.
// AsyncCollectionResult<StreamingChatCompletionUpdate> completionUpdates = agent.RunStreamingAsync([chatMessage]);
// await foreach (StreamingChatCompletionUpdate completionUpdate in completionUpdates)
// {
//     if (completionUpdate.ContentUpdate.Count > 0)
//     {
//         Console.WriteLine(completionUpdate.ContentUpdate[0].Text);
//     }
// }

// run loop
// while (true)
// {
//     Console.WriteLine("Enter your message (or 'exit' to quit):");
//     string? userInput = Console.ReadLine();
//     if (userInput == null || userInput.Equals("exit", StringComparison.OrdinalIgnoreCase))
//     {
//         break;
//     }

//     UserChatMessage userMessage = new(userInput);

//     ChatCompletion response = await agent.RunAsync([userMessage]);
//     Console.WriteLine("Agent response: " + response.Content.Last().Text);
// }   