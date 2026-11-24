$ErrorActionPreference='Stop'
$data=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'motions.json') -Raw|ConvertFrom-Json
if($data.clips.Count -ne 156){throw 'Missing draft variants'}
if(@($data.clips|Group-Object id|Where-Object Count -gt 1).Count){throw 'Duplicate clip IDs'}
$samples=0
foreach($clip in $data.clips){
 $last=-1.
 foreach($frame in $clip.frames){
  if($frame.t -le $last){throw "Non-monotonic motion: $($clip.id)"};$last=$frame.t
  if($clip.family -eq 'shoo'){
   if($frame.Blend -lt 0 -or $frame.Blend -gt 1){throw 'Shoo blend out of range'}
  }else{
   foreach($value in $frame.head){if([double]::IsNaN($value) -or [double]::IsInfinity($value)){throw 'Invalid contact coordinate'}}
   if($clip.family -eq 'sweep'){
    if([Math]::Abs($frame.head[0]) -gt $clip.rugBounds[0]/2-.1 -or [Math]::Abs($frame.head[2]) -gt $clip.rugBounds[1]/2-.1 -or $frame.head[1] -lt 0){throw 'Sweep leaves clear rug footprint'}
   }elseif($clip.orientation -eq 'vertical'){
    if($frame.head[2] -lt $clip.bounds[2]/2){throw 'Dust trajectory enters display bounding box'}
   }elseif($frame.head[1] -lt $clip.height+$clip.bounds[1]){throw 'Dust trajectory enters display bounding box'}
  }
  $samples++
 }
 if([Math]::Abs($last-$clip.duration) -gt .001){throw "Incomplete duration: $($clip.id)"}
}
"Draft checks passed: 156 motion samples, $samples frames; unique IDs, time ranges, shoo blends, conservative display bounds and rug footprints. This does not verify skeletal/tool clipping in game."
