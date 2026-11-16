using System.Text.Json.Serialization;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;

namespace OnlyHsVote;

// !onlyhs oylaması (kullanıcı istedi, 2026-10-23): sunucuda en az 2 oyuncu varken biri !onlyhs yazınca oylama açılır,
// herkes !aç / !kapat ile oy verir (başlatan "değişsin" sayılır; 2026-10-24 kullanıcı: !evet/!hayir yerine !aç/!kapat). Süre bitince evet hayırdan fazlaysa sadece kafa vuruşu
// (mp_damage_headshot_only) açılır; açıkken yapılan oylama kapatır. Her harita kapalı başlar.
public class OnlyHsConfig : BasePluginConfig
{
    [JsonPropertyName("MinPlayers")] public int MinPlayers { get; set; } = 2;
    [JsonPropertyName("VoteSeconds")] public int VoteSeconds { get; set; } = 20;
    [JsonPropertyName("CooldownSeconds")] public int CooldownSeconds { get; set; } = 60;
}

public class OnlyHsVote : BasePlugin, IPluginConfig<OnlyHsConfig>
{
    public override string ModuleName => "OnlyHsVote";
    public override string ModuleVersion => "1.3.0";
    public override string ModuleAuthor => "Claude";
    public override string ModuleDescription => "!onlyhs oylamasıyla sadece kafa vuruşu modunu açar/kapatır.";

    public OnlyHsConfig Config { get; set; } = new();
    public void OnConfigParsed(OnlyHsConfig config) => Config = config;

    private const string Tag = " \x04[OnlyHS]\x01";
    private bool _on;                                  // şu an sadece kafa vuruşu açık mı
    private bool _voting;
    private readonly Dictionary<ulong, bool> _votes = new();
    private DateTime _nextVote = DateTime.MinValue;
    private CounterStrikeSharp.API.Modules.Timers.Timer? _timer;
    private CounterStrikeSharp.API.Modules.Timers.Timer? _hud;   // ekranın ortasındaki yazıyı saniyede bir günceller
    private DateTime _endsAt;
    private string _hudHtml = "";   // CS2 ortadaki HTML yazıyı bir an gösteriyor: her karede (OnTick) yeniden gönderilir

    public override void Load(bool hotReload)
    {
        // Önceki haritanın oylama zamanlayıcıları durdurulur; yoksa yeni haritadaki oylamayı vaktinden önce bitirebilir (2026-11-16)
        RegisterListener<Listeners.OnMapStart>(_ => { _voting = false; _votes.Clear(); _timer?.Kill(); _timer = null; _hud?.Kill(); _hud = null; _hudHtml = ""; SetMode(false); });
        if (hotReload) SetMode(false);
        // Ortadaki yazı her karede gönderilmezse anlık görünüp kayboluyordu (kullanıcı 2026-10-24)
        RegisterListener<Listeners.OnTick>(() =>
        {
            if (!_voting || _hudHtml == "") return;
            foreach (var p in Humans()) p.PrintToCenterHtml(_hudHtml);
        });
        // "!aç" Türkçe harfli komut; kaydedilemezse eklenti yine çalışsın (!ac her zaman var)
        try { AddCommand("css_aç", "Oylamada: kafa vuruşu açık olsun", (p, i) => Cast(p, true)); }
        catch (Exception e) { Console.WriteLine("[OnlyHsVote] css_aç kaydedilemedi: " + e.Message); }
    }

    private static List<CCSPlayerController> Humans() =>
        Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot && !p.IsHLTV && p.Connected == PlayerConnectedState.Connected).ToList();

    private void SetMode(bool on)
    {
        _on = on;
        Server.ExecuteCommand("mp_damage_headshot_only " + (on ? "1" : "0"));
    }

    [ConsoleCommand("css_onlyhs", "Sadece kafa vuruşu oylaması başlatır")]
    public void OnOnlyHs(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || !player.IsValid) return;
        if (_voting) { Vote(player, true); return; }
        var humans = Humans().Count;
        if (humans < Config.MinPlayers)
        {
            player.PrintToChat($"{Tag} Oylama için sunucuda en az {Config.MinPlayers} oyuncu olmalı.");
            return;
        }
        var wait = (_nextVote - DateTime.UtcNow).TotalSeconds;
        if (wait > 0)
        {
            player.PrintToChat($"{Tag} Yeni oylama için {Math.Ceiling(wait)} sn bekle.");
            return;
        }
        _voting = true;
        _votes.Clear();
        _votes[player.SteamID] = true;
        var what = _on ? "KAPATILSIN" : "AÇILSIN";
        Server.PrintToChatAll($"{Tag} {ChatColors.Gold}{player.PlayerName}{ChatColors.Default} oylama başlattı: sadece kafa vuruşu {ChatColors.Green}{what}{ChatColors.Default} mı?");
        Server.PrintToChatAll($"{Tag} Oy ver: {ChatColors.Green}!aç{ChatColors.Default} (açık olsun) ya da {ChatColors.Red}!kapat{ChatColors.Default} (kapalı olsun) ({Config.VoteSeconds} sn)");
        _endsAt = DateTime.UtcNow.AddSeconds(Config.VoteSeconds);
        _timer = AddTimer(Config.VoteSeconds, Finish);
        // Oylama sürerken ekranın ortasında: soru, kalan süre, evet/hayır sayısı (kullanıcı 2026-10-24 istedi)
        _hud = AddTimer(1.0f, ShowHud, CounterStrikeSharp.API.Modules.Timers.TimerFlags.REPEAT);
        ShowHud();
    }

    // Oyuncular "!only hs" (boşluklu) ve "!hs" de yazıyor (sohbet kaydı 2026-10-24)
    [ConsoleCommand("css_only", "!only hs = !onlyhs")]
    public void OnOnly(CCSPlayerController? player, CommandInfo info)
    {
        if (info.ArgCount > 1 && info.GetArg(1).Equals("hs", StringComparison.OrdinalIgnoreCase)) OnOnlyHs(player, info);
    }

    [ConsoleCommand("css_hs", "!hs = !onlyhs")]
    public void OnHs(CCSPlayerController? player, CommandInfo info) => OnOnlyHs(player, info);

    // !aç = kafa vuruşu açık olsun, !kapat = kapalı olsun; oylamanın yönüne göre "değişsin" ya da "kalsın" sayılır
    [ConsoleCommand("css_ac", "Oylamada: kafa vuruşu açık olsun")]
    public void OnOpen(CCSPlayerController? player, CommandInfo info) => Cast(player, true);

    [ConsoleCommand("css_kapat", "Oylamada: kafa vuruşu kapalı olsun")]
    public void OnClose(CCSPlayerController? player, CommandInfo info) => Cast(player, false);

    // Eski komutlar alışkanlıkla yazılırsa yine çalışsın (evet = değişsin, hayır = kalsın)
    [ConsoleCommand("css_evet", "Açık oylamada evet")]
    public void OnYes(CCSPlayerController? player, CommandInfo info) => Vote(player, true);

    [ConsoleCommand("css_hayir", "Açık oylamada hayır")]
    public void OnNo(CCSPlayerController? player, CommandInfo info) => Vote(player, false);

    private void Cast(CCSPlayerController? player, bool wantOn) => Vote(player, wantOn != _on);

    private void Vote(CCSPlayerController? player, bool yes)
    {
        if (player == null || !player.IsValid || player.IsBot) return;
        if (!_voting) { player.PrintToChat($"{Tag} Şu an oylama yok. Başlatmak için !onlyhs yaz."); return; }
        _votes[player.SteamID] = yes;
        var y = _votes.Values.Count(v => v);
        player.PrintToChat($"{Tag} Oyun alındı: {((yes != _on) ? "açık olsun" : "kapalı olsun")}. ({Counts()})");
        ShowHud();
    }

    private void ShowHud()
    {
        if (!_voting) return;
        var left = Math.Max(0, (int)Math.Ceiling((_endsAt - DateTime.UtcNow).TotalSeconds));
        var (on, off) = OnOff();
        _hudHtml = $"<font color='#ffcc00'><b>OnlyHS oylaması</b></font><br>Sadece kafa vuruşu <b>{(_on ? "KAPATILSIN" : "AÇILSIN")}</b> mı?<br>"
                 + $"<font color='#7CFC00'>!aç: {on}</font> &nbsp; <font color='#ff5555'>!kapat: {off}</font><br>"
                 + $"Kalan: <b>{left} sn</b>";
    }

    // Oylar "değişsin" (true) / "kalsın" olarak tutulur; ekranda açık olsun / kapalı olsun diye gösterilir
    private (int on, int off) OnOff()
    {
        var change = _votes.Values.Count(v => v);
        var keep = _votes.Count - change;
        return _on ? (keep, change) : (change, keep);
    }
    private string Counts() { var (on, off) = OnOff(); return $"!aç {on} - !kapat {off}"; }

    private void Finish()
    {
        if (!_voting) return;
        _voting = false;
        _timer = null;
        _hud?.Kill();
        _hud = null;
        _hudHtml = "";
        _nextVote = DateTime.UtcNow.AddSeconds(Config.CooldownSeconds);
        // Oyundan çıkanların oyu sayılmaz
        var present = Humans().Select(p => p.SteamID).ToHashSet();
        var yes = _votes.Count(kv => kv.Value && present.Contains(kv.Key));
        var no = _votes.Count(kv => !kv.Value && present.Contains(kv.Key));
        _votes.Clear();
        var counts = _on ? $"!aç {no} - !kapat {yes}" : $"!aç {yes} - !kapat {no}";
        if (yes > no)
        {
            SetMode(!_on);
            Server.PrintToChatAll($"{Tag} Oylama kabul edildi ({counts}). Sadece kafa vuruşu {(_on ? $"{ChatColors.Green}AÇIK" : $"{ChatColors.Red}KAPALI")}{ChatColors.Default}.");
        }
        else
        {
            Server.PrintToChatAll($"{Tag} Oylama reddedildi ({counts}). Oyun olduğu gibi devam ediyor.");
        }
    }
}
