using System;
using System.Collections.Generic;
using System.Linq;

public class Item
{
    public DateTime Timestamp { get; set; }
    public string Name { get; set; }
}

class Program
{
    static void Main()
    {
        var items = new List<Item>
        {
            new Item { Timestamp = DateTime.Parse("2025-08-17 09:05"), Name = "A" },
            new Item { Timestamp = DateTime.Parse("2025-08-17 09:20"), Name = "B" },
            new Item { Timestamp = DateTime.Parse("2025-08-17 09:40"), Name = "C" },
            new Item { Timestamp = DateTime.Parse("2025-08-17 10:10"), Name = "D" }
        };

        int intervalMinutes = 23;

        // Step 1: Get min and max
        DateTime minTime = items.Min(x => x.Timestamp);
        DateTime maxTime = items.Max(x => x.Timestamp);

        // Step 2: Create dynamic "time slots"
        var slots = new List<DateTime>();
        DateTime current = minTime;
        while (current <= maxTime)
        {
            slots.Add(current);
            current = current.AddMinutes(intervalMinutes);
        }

        // Step 3: Group items into slots
        var grouped = slots.Select(slot => new
        {
            SlotStart = slot,
            Items = items.Where(i =>
                i.Timestamp >= slot &&
                i.Timestamp < slot.AddMinutes(intervalMinutes)).ToList()
        })
        .Where(g => g.Items.Any()) // keep only non-empty slots
        .ToList();

        // Step 4: Print
        foreach (var group in grouped)
        {
            Console.WriteLine($"{group.SlotStart:HH:mm} -> {string.Join(", ", group.Items.Select(i => i.Name))}");
        }
    }
}
