using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class JsonHelper
{
    public static List<JObject> GetAllOriginalEvents(string json)
    {
        var result = new List<JObject>();

        // Parse JSON into a JArray (list of objects)
        var array = JArray.Parse(json);

        foreach (var item in array)
        {
            var originalEvent = item["originalEvent"] as JObject;
            if (originalEvent != null)
            {
                result.Add(originalEvent);
            }
        }

        return result;
    }
}

class Program
{
    static void Main()
    {
        string json = File.ReadAllText("data.json");
        List<JObject> originalEvents = JsonHelper.GetAllOriginalEvents(json);

        Console.WriteLine($"Found {originalEvents.Count} originalEvent nodes.\n");
        foreach (var ev in originalEvents)
        {
            Console.WriteLine(ev.ToString());
            Console.WriteLine(new string('-', 40));
        }
    }
}
