using System;
using System.Threading;
using System.Web.Script.Serialization;
using System.Collections.Generic;

namespace CodexUsageOverlay.Tests
{
    public static class FakeServer
    {
        public static int Run(string[] args)
        {
            string mode = args.Length > 1 ? args[1] : "normal";
            var json = new JavaScriptSerializer();
            string first = Console.ReadLine(); if (first == null) return 0;
            var request = (IDictionary<string, object>)json.DeserializeObject(first);
            if (mode == "timeout") { Thread.Sleep(20000); return 0; }
            if (mode == "eof") return 0;
            if (mode == "malformed") { Console.WriteLine("not json"); return 0; }
            if (mode == "oversized") { Console.WriteLine(new string('x', 5000)); return 0; }
            if (mode == "reverse")
            {
                var second = (IDictionary<string, object>)json.DeserializeObject(Console.ReadLine());
                Console.WriteLine("{\"method\":\"account/rateLimits/updated\",\"params\":{\"rateLimits\":{\"limitId\":\"codex\"}}}");
                Reply(json, second); Reply(json, request);
                while (Console.ReadLine() != null) { }
                return 0;
            }
            Reply(json, request);
            string line; while ((line = Console.ReadLine()) != null) Reply(json, (IDictionary<string, object>)json.DeserializeObject(line));
            return 0;
        }
        static void Reply(JavaScriptSerializer json, IDictionary<string, object> request)
        {
            object id; if (!request.TryGetValue("id", out id)) return;
            Console.WriteLine(json.Serialize(new { id = id, result = new { echo = request.ContainsKey("params") ? request["params"] : null } }));
        }
    }
}
