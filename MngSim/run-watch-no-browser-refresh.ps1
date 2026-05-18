# dotnet watch ile geliştirme; Browser Refresh tamamen devre dışı.
# train-map.html / API isteklerinde "unsupported compression method" hatası oluşmaz.
# Kullanım: .\run-watch-no-browser-refresh.ps1
$env:DOTNET_WATCH_SUPPRESS_BROWSER_REFRESH = '1'
# Hosting startup ile enjekte edilen middleware'i yükleme (compression hatasını keser)
$env:ASPNETCORE_HOSTINGSTARTUPEXCLUDEASSEMBLIES = 'Microsoft.AspNetCore.Watch.BrowserRefresh'
dotnet watch run
