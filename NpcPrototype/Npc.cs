using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;

namespace NpcPrototype;

/// <summary>
/// Oyun dünyasındaki tekil bir NPC'yi temsil eder; isim, karakter, ruh hali,
/// kısa süreli (conversation) ve uzun süreli (fact) hafıza tutar.
/// Claude API üzerinden konuşur.
/// </summary>
public class Npc(string name, string personality)
{
    private readonly AnthropicClient _client = new();

    /// <summary>NPC'nin görünen adı.</summary>
    public string Name { get; } = name;

    /// <summary>NPC'nin kişilik tanımı (ör. "neşeli fırıncı, biraz dedikoducu").</summary>
    public string Personality { get; } = personality;

    /// <summary>NPC'nin o anki ruh hali; varsayılan "normal".</summary>
    public string Mood { get; set; } = "normal";

    /// <summary>
    /// Kısa süreli hafıza — son konuşma geçmişi. API'ye messages[] olarak gider.
    /// Maksimum 10 kayıt (TrimMemory).
    /// </summary>
    public List<string> Memory { get; } = [];

    /// <summary>
    /// Uzun süreli hafıza — konuşmalardan çıkarılmış kalıcı fact'ler.
    /// System prompt'a eklenir. Maksimum 20 kayıt (TrimLongTermMemory).
    /// </summary>
    public List<string> LongTermMemory { get; } = [];

    /// <summary>
    /// Oyuncunun bir şey söylemesini Claude'a gönderir, NPC'nin cevabını alır ve hafızaya ekler.
    /// </summary>
    public async Task<string> TalkAsync(string playerInput)
    {
        try
        {
            var messages = BuildMessagesFromMemory();
            messages.Add(new MessageParam
            {
                Role = Role.User,
                Content = playerInput,
            });

            var systemPrompt = BuildSystemPrompt();

            var response = await _client.Messages.Create(new MessageCreateParams
            {
                Model = Model.ClaudeHaiku4_5,
                MaxTokens = 200,
                System = systemPrompt,
                Messages = messages,
            });

            var reply = string.Concat(
                response.Content
                    .Select(b => b.Value)
                    .OfType<TextBlock>()
                    .Select(t => t.Text));

            Memory.Add($"Oyuncu dedi: {playerInput}");
            Memory.Add($"Ben dedim: {reply}");
            TrimMemory();
            return reply;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[API Hatası] {ex.Message}");
            return $"...{Name} cevap vermedi, bir sorun var";
        }
    }

    /// <summary>
    /// Son konuşma kayıtlarından kalıcı fact'leri çıkarır ve LongTermMemory'ye ekler.
    /// Ayrı bir API çağrısı yapar — konuşma akışından bağımsızdır.
    /// </summary>
    public async Task ExtractFactsAsync()
    {
        var recentEntries = Memory.TakeLast(5).ToList();
        if (recentEntries.Count == 0) return;

        var conversationText = string.Join("\n", recentEntries);

        try
        {
            var response = await _client.Messages.Create(new MessageCreateParams
            {
                Model = Model.ClaudeHaiku4_5,
                MaxTokens = 300,
                System = """
                    Aşağıdaki konuşmadan, ileride hatırlanması gereken önemli bilgileri
                    JSON array olarak çıkar. Sadece isim, tercih, önemli olay gibi şeyler.
                    Önemsiz selamlaşma gibi şeyleri dahil etme. Hiç yoksa boş array dön.
                    Örnek çıktı: ["Adı Musa", "Çilekli pasta sever"]
                    Sadece JSON array dön, başka bir şey yazma.
                    """,
                Messages = [
                    new MessageParam
                    {
                        Role = Role.User,
                        Content = conversationText,
                    }
                ],
            });

            var json = string.Concat(
                response.Content
                    .Select(b => b.Value)
                    .OfType<TextBlock>()
                    .Select(t => t.Text))
                .Trim();

            var facts = JsonSerializer.Deserialize<List<string>>(json);
            if (facts is null) return;

            foreach (var fact in facts)
            {
                var trimmed = fact.Trim();
                if (trimmed.Length == 0) continue;

                // Duplicate kontrolü: mevcut fact'lerle büyük/küçük harf duyarsız karşılaştır
                bool alreadyKnown = LongTermMemory.Any(existing =>
                    existing.Equals(trimmed, StringComparison.OrdinalIgnoreCase));

                if (!alreadyKnown)
                {
                    LongTermMemory.Add(trimmed);
                }
            }

            TrimLongTermMemory();

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"  [{Name} {facts.Count} fact çıkardı, toplam {LongTermMemory.Count} fact biliniyor]");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Fact Extraction Hatası] {ex.Message}");
        }
    }

    /// <summary>NPC'ye bir olay/bilgi hatırlatır ve hafızaya ekler.</summary>
    public void Remember(string fact)
    {
        Memory.Add(fact);
        TrimMemory();
    }

    /// <summary>Kısa süreli hafızayı 10 kayıtla sınırlar.</summary>
    public void TrimMemory()
    {
        const int maxMemory = 10;
        if (Memory.Count > maxMemory)
        {
            Memory.RemoveRange(0, Memory.Count - maxMemory);
        }
    }

    /// <summary>Uzun süreli hafızayı 20 kayıtla sınırlar.</summary>
    public void TrimLongTermMemory()
    {
        const int maxLongTermMemory = 20;
        if (LongTermMemory.Count > maxLongTermMemory)
        {
            LongTermMemory.RemoveRange(0, LongTermMemory.Count - maxLongTermMemory);
        }
    }

    /// <summary>
    /// System prompt'u oluşturur. LongTermMemory doluysa fact'leri de ekler.
    /// </summary>
    private string BuildSystemPrompt()
    {
        var basePrompt = $"Sen {Name}, {Personality}. Kısa, doğal cevaplar ver, en fazla 2 cümle. Türkçe konuş.";

        if (LongTermMemory.Count == 0)
            return basePrompt;

        var facts = string.Join("\n", LongTermMemory.Select(f => $"- {f}"));
        return $"""
            {basePrompt}

            Bu kişi hakkında bildiklerin:
            {facts}
            """;
    }

    /// <summary>
    /// Hafıza kayıtlarını Claude API'nin beklediği user/assistant mesaj listesine çevirir.
    /// </summary>
    private List<MessageParam> BuildMessagesFromMemory()
    {
        var messages = new List<MessageParam>();
        bool foundFirstUser = false;

        foreach (var entry in Memory)
        {
            if (entry.StartsWith("Oyuncu dedi: "))
            {
                foundFirstUser = true;
                messages.Add(new MessageParam
                {
                    Role = Role.User,
                    Content = entry["Oyuncu dedi: ".Length..],
                });
            }
            else if (entry.StartsWith("Ben dedim: ") && foundFirstUser)
            {
                messages.Add(new MessageParam
                {
                    Role = Role.Assistant,
                    Content = entry["Ben dedim: ".Length..],
                });
            }
        }

        return messages;
    }
}
