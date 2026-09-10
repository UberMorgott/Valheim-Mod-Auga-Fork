# Model check for AugaHealthBar FastBar.m_changeDelay = 0 (plan T8).
# Fails on the old behaviour (delay 0.5 re-armed every frame), passes on the fix.
Add-Type -TypeDefinition @'
public static class BarSim {
  // Mirrors GuiBar.SetValue/LateUpdate fill path (assembly_guiutils GuiBar.cs:57-116).
  public static float Run(float changeDelay, int frames) {
    float dt = 1f/60f, max = 100f, value = 50f, smooth = 0.5f, timer = 0f, speed = 0.25f;
    for (int f = 0; f < frames; f++) {
      float nv = value + 0.05f;               // SmoothRegen: +3 HP/s paid per frame
      if (nv > value) timer = changeDelay;    // SetValue re-arms delay (smoothFill)
      value = nv;
      timer -= dt;
      if (timer <= 0f) { float t = value / max; smooth = t > smooth ? System.Math.Min(t, smooth + speed * dt) : t; }
    }
    return smooth * max - value;              // red fill minus real HP
  }
}
'@
$bug = [BarSim]::Run(0.5, 600); $fix = [BarSim]::Run(0, 600)
if ($bug -gt -29) { throw "model does not reproduce the frozen bar: lag=$bug" }
if ([math]::Abs($fix) -gt 0.1) { throw "delay 0 still lags: $fix" }
"OK frozen-lag=$bug fixed-lag=$fix"
