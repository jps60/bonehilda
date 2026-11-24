param([string]$AssemblyPath = (Join-Path $PSScriptRoot '../../src/Bonehilda/bin/Release/net462/Bonehilda.dll'))
$ErrorActionPreference='Stop'
$assembly=[Reflection.Assembly]::LoadFrom([IO.Path]::GetFullPath($AssemblyPath))
$sample=$assembly.GetType('Bonehilda.ShooMotion').GetMethod('Sample',[Reflection.BindingFlags]'Static,NonPublic')
$flags=[Reflection.BindingFlags]'Instance,NonPublic'
$clips=New-Object Collections.Generic.List[object]
foreach($variant in 0..2){
 $frames=@(for($index=0;$index -le 72;$index++){$pose=$sample.Invoke($null,@([int]$variant,[float]($index/30)));$frame=[ordered]@{t=$index/30};foreach($field in $pose.GetType().GetFields($flags)){$frame[$field.Name]=$field.GetValue($pose)};$frame})
 $clips.Add([ordered]@{id=@('shoo_left','shoo_right','shoo_both')[$variant];family='shoo';duration=2.4;status='playable procedural draft';frames=$frames})
}
$heights=@(@{id='horizontal_floor';y=.15;orientation='horizontal'},@{id='horizontal_low_table';y=.55;orientation='horizontal'},@{id='horizontal_table';y=.95;orientation='horizontal'},@{id='horizontal_head';y=1.7;orientation='horizontal'},@{id='vertical_chest';y=1.2;orientation='vertical'},@{id='vertical_overhead';y=1.9;orientation='vertical'})
$shapes=@(@{id='long_narrow';w=1.1;h=.12;d=.15},@{id='broad_flat';w=.8;h=.10;d=.6},@{id='round';w=.5;h=.45;d=.5},@{id='irregular';w=.6;h=.6;d=.4},@{id='bulky';w=.65;h=.7;d=.55})
$families=@('short_flicks','broad_sweep','rim_follow','vertical_wipe','overhead_arc')
foreach($family in $families){foreach($height in $heights){foreach($shape in $shapes){
 $frames=@(for($index=0;$index -le 120;$index++){
  $time=$index/30;$phase=[Math]::PI*4*$time/4
  switch($family){
   short_flicks{$u=.28*[Math]::Sin($phase*2);$v=.15*[Math]::Cos($phase)}
   broad_sweep{$u=.42*[Math]::Sin($phase);$v=.20*[Math]::Sin($phase/2)}
   rim_follow{$u=.44*[Math]::Cos($phase);$v=.44*[Math]::Sin($phase)}
   vertical_wipe{$u=.16*[Math]::Sin($phase);$v=.42*[Math]::Sin($phase*1.5)}
   overhead_arc{$u=.40*[Math]::Sin($phase);$v=.30*[Math]::Cos($phase)}
  }
  $head=if($height.orientation -eq 'vertical'){@(($u*$shape.w),($height.y+$v*$shape.h),($shape.d/2+.025))}else{@(($u*$shape.w),($height.y+$shape.h+.025),($v*$shape.d))}
  [ordered]@{t=$time;head=$head;contact=($time -ge .4 -and $time -le 3.6)}
 })
 $clips.Add([ordered]@{id="dust_${family}_$($height.id)_$($shape.id)";family='dust';motion=$family;mount=$height.id;shape=$shape.id;bounds=@($shape.w,$shape.h,$shape.d);height=$height.y;orientation=$height.orientation;duration=4;status='task-space trajectory draft; not integrated into NPC target selection';frames=$frames})
}}}
foreach($variant in 0..2){
 $frames=@(for($index=0;$index -le 150;$index++){
  $time=$index/30;$phase=$time/5*6*[Math]::PI
  switch($variant){0{$x=.26*[Math]::Sin($phase);$z=.38*[Math]::Cos($phase)}1{$x=.48*[Math]::Sin($phase);$z=.16*[Math]::Cos($phase*2)}2{$x=.46*[Math]::Sin($phase);$z=.34*[Math]::Sin($phase+.6)}}
  [ordered]@{t=$time;head=@($x,.015,$z);leftHand=@(($x*.20-.08),1.1,($z*.20+.20));rightHand=@(($x*.30+.08),.85,($z*.25+.36));contact=($time -ge .4 -and $time -le 4.6)}
 })
 $clips.Add([ordered]@{id=@('sweep_long_draw','sweep_short_gather','sweep_diagonal_fan')[$variant];family='sweep';duration=5;tool='Native Kvastur model; exact grip/bristle calibration pending review';rugBounds=@(1.4,1.0);status='task-space draft, no automatic rug sweeping';frames=$frames})
}
$data=[ordered]@{schema='Bonehilda.task-space-animation-drafts.v1';fps=30;units='meters; X right, Y up, Z toward the target';note='Three runtime shoo poses plus sampled cleaning trajectories. Cleaning samples are not baked skeletal clips and do not establish clipping-free runtime playback.';dustFamilies=$families;mounts=$heights;shapes=$shapes;clips=$clips}
$json=$data|ConvertTo-Json -Depth 10 -Compress
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'motions.json'),$json,(New-Object Text.UTF8Encoding($false)))
$template=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'preview-template.html'))
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'preview.html'),$template.Replace('__MOTIONS__',$json),(New-Object Text.UTF8Encoding($false)))
"Generated $($clips.Count) samples: 3 shoo poses, 150 dust trajectories from 5 families / 6 mounting heights / 5 shapes, 3 sweeps."
