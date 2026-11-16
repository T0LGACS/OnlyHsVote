# OnlyHsVote

Sadece kafa vuruşu (only HS) oylaması. Sunucuda en az 2 kişi varken açılır; oylama süresince ekranın ortasında soru, kalan süre ve oylar görünür. Her harita kapalı başlar.

> Headshot-only vote with an on-screen live tally.

[YCC TEAM](https://yccteam.com.tr) CS2 sunucusu için sıfırdan yazıldı ve sunucuda her gün çalışıyor. Başka eklentilerden kod alınmadı.

## Gereken

- CS2 sunucusu, [Metamod:Source](https://www.sourcemm.net/) ve [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp) (1.0.371 ve üstü)

## Kurulum

1. Sunucuda [Metamod:Source](https://www.sourcemm.net/downloads.php?branch=dev) ve [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp/releases) (with-runtime sürümü) kurulu olmalı.
2. [.NET 10 SDK](https://dotnet.microsoft.com/download) ile derle: `dotnet build -c Release`
3. `bin/Release/net10.0/` içindeki her şeyi sunucuda `addons/counterstrikesharp/plugins/OnlyHsVote/` klasörüne kopyala.
4. Sunucuyu başlat. Ayar dosyası ilk açılışta `addons/counterstrikesharp/configs/plugins/OnlyHsVote/OnlyHsVote.json` olarak kendiliğinden oluşur; düzenleyip sunucuyu yeniden başlat.

## Komutlar

Sohbette `!` ile, konsolda `css_` ile yazılır.

| Komut | Ne yapar |
|---|---|
| `!onlyhs (ya da !hs, !only hs)` | Oylama başlatır |
| `!aç / !ac` | Kafa vuruşu açık olsun |
| `!kapat` | Kafa vuruşu kapalı olsun |

## Lisans

MIT. Kullanabilir, değiştirebilir, paylaşabilirsin; lisans dosyasını koruman yeterli.

Sunucu: **yccteam.com.tr** · Site: https://yccteam.com.tr
