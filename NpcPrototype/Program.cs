using NpcPrototype;

var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
if (string.IsNullOrEmpty(apiKey))
{
    Console.Error.WriteLine("HATA: ANTHROPIC_API_KEY environment variable tanımlı değil.");
    Console.Error.WriteLine("PowerShell örneği: $env:ANTHROPIC_API_KEY=\"sk-ant-...\"");
    return 1;
}

var npcs = new Dictionary<string, Npc>(StringComparer.OrdinalIgnoreCase)
{
    ["ayse"] = new Npc("Ayşe", "neşeli fırıncı, biraz dedikoducu"),
    ["kemal"] = new Npc("Kemal",
        "huysuz demirci, az konuşur, kısa ve sert cevaplar verir ama alttan alta bilge " +
        "ve yardımseverdir. Laf kalabalığından hoşlanmaz. Max 1-2 cümle."),
    ["elif"] = new Npc("Elif",
        "12 yaşında meraklı bir çocuk, her şeye 'neden?' diye sorar, heyecanlı ve enerjik " +
        "konuşur, bazen saçma teoriler uydurur. Max 2 cümle. " +
        "Bazen diğer NPC'ler hakkında dedikodu yapar — Ayşe'nin ekmeği, Kemal'in huysuzluğu gibi."),
};

// Her NPC için ayrı mesaj sayacı
var messageCounts = npcs.Keys.ToDictionary(k => k, _ => 0, StringComparer.OrdinalIgnoreCase);

Console.WriteLine("Köye hoşgeldin! Her NPC'nin kendi hafızası var.\n");

while (true)
{
    Console.Write("Kiminle konuşmak istersin? (ayse/kemal/elif/exit): ");
    var choice = Console.ReadLine()?.Trim().ToLowerInvariant();

    if (choice is null) break;
    if (choice == "exit") break;
    if (string.IsNullOrWhiteSpace(choice)) continue;

    if (!npcs.TryGetValue(choice, out var npc))
    {
        Console.WriteLine("Böyle biri yok köyde. Tekrar dene.\n");
        continue;
    }

    Console.WriteLine($"\n{npc.Name} ile konuşuyorsun. Başkasıyla konuşmak için 'degistir', çıkmak için 'exit' yaz.\n");

    bool exitProgram = false;

    while (true)
    {
        Console.Write("> ");
        var input = Console.ReadLine();

        if (input is null) { exitProgram = true; break; }
        if (string.IsNullOrWhiteSpace(input)) continue;

        var trimmed = input.Trim();
        if (trimmed.Equals("exit", StringComparison.OrdinalIgnoreCase))
        {
            exitProgram = true;
            break;
        }
        if (trimmed.Equals("degistir", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine();
            break;
        }

        var reply = await npc.TalkAsync(input);
        Console.WriteLine($"{npc.Name}: {reply}\n");

        messageCounts[choice]++;
        if (messageCounts[choice] % 5 == 0)
        {
            await npc.ExtractFactsAsync();
        }
    }

    if (exitProgram) break;
}

// Çıkışta tüm NPC'lerin uzun süreli hafızalarını göster
foreach (var (_, npc) in npcs)
{
    if (npc.LongTermMemory.Count > 0)
    {
        Console.WriteLine($"\n{npc.Name}'nin uzun süreli hafızası:");
        foreach (var fact in npc.LongTermMemory)
        {
            Console.WriteLine($"  - {fact}");
        }
    }
}

return 0;
