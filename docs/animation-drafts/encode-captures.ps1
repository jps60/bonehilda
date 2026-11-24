param(
    [Parameter(Mandatory=$true)][string]$InputDirectory,
    [Parameter(Mandatory=$true)][string]$FfmpegPath,
    [string]$OutputDirectory = ''
)
$ErrorActionPreference='Stop'
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $PSScriptRoot 'captures' }
New-Item -ItemType Directory -Force $OutputDirectory | Out-Null
$clips=@('shoo-left','shoo-right','shoo-both','sweep-long','sweep-gather','sweep-fan')
foreach($clip in $clips){
    $inputPattern=Join-Path $InputDirectory "$clip-%03d.png"
    $outputPath=Join-Path $OutputDirectory "$clip.mp4"
    & $FfmpegPath -hide_banner -loglevel error -y -framerate 12 -i $inputPattern -an -c:v libx264 -crf 20 -pix_fmt yuv420p -movflags +faststart $outputPath
    if($LASTEXITCODE -ne 0){throw "Failed to encode $clip"}
}
$media=@{}
foreach($clip in $clips){$media[$clip]=[Convert]::ToBase64String([IO.File]::ReadAllBytes((Join-Path $OutputDirectory "$clip.mp4")))}
$json=$media|ConvertTo-Json -Compress
$html=@'
<!doctype html><html><head><meta charset="utf-8"><style>
body{font:14px system-ui;color:var(--text-primary,#ddd);margin:0}h2{font-size:19px;margin:0 0 8px}p{line-height:1.45}select{font:inherit;padding:6px}video{display:block;width:100%;max-height:440px;background:#1b242b;margin-top:10px}small{color:var(--text-secondary,#aaa)}
</style></head><body><h2>Bonehilda — native animation review</h2><select id="motion"></select><video id="video" controls loop muted playsinline></video><p id="note"></p><small>Actual Valheim skeleton, clothing, native player controller and runtime poses. Front and side cameras are synchronized. Shoos sample the native walk loop on a stationary private stage; this is not a navigation or multiplayer test. Tests are muted; normal-play skeleton sound timing remains for review. Sweeping remains a manual preview.</small><script>
const clips=__MEDIA__,pick=document.getElementById('motion'),video=document.getElementById('video');
for(const id of Object.keys(clips)){const option=document.createElement('option');option.value=id;option.textContent=id.replaceAll('-',' ');pick.append(option)}
function choose(){video.src='data:video/mp4;base64,'+clips[pick.value];document.getElementById('note').textContent=pick.value.startsWith('shoo')?'Review the invitation, smaller second flick, relaxed elbow, wrist direction and recovery into stride.':'Review the scaled Kvastur, both palms on the shaft, finger wrap, working stroke, lifted reset, rug contact and dress clearance.';video.play().catch(()=>{})}pick.onchange=choose;choose();
</script></body></html>
'@
$html=$html.Replace('__MEDIA__',$json)
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'native-review.html'),$html,(New-Object Text.UTF8Encoding($false)))
Copy-Item -LiteralPath (Join-Path $InputDirectory 'measurements.csv') -Destination $OutputDirectory -Force
Write-Output 'Encoded six native clips and a self-contained review player.'
