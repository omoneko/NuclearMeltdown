# Nuclear Meltdown (Cities: Skylines Mod)

原子力発電所が全焼または崩壊すると、隕石爆発エフェクトと広範囲の疑似放射能汚染（土壌汚染）を発生させる。汚染はゲーム内50年経過、または除染施設（既定: 下水処理施設 Water Treatment Plant）の稼働で消滅する。

## 依存
- Harmony (Mod Dependency) — Steam Workshop の CitiesHarmony を購読しておくこと。

## ビルドと配置
```powershell
powershell -ExecutionPolicy Bypass -File build.ps1
```
`%LOCALAPPDATA%\Colossal Order\Cities_Skylines\Addons\Mods\NuclearMeltdown\` に配置される。

## ゲーム内動作確認手順
1. 起動 → コンテンツマネージャ → Mods で "Nuclear Meltdown" を有効化。
2. Harmony が有効であること（ログに `[NuclearMeltdown] Harmony patches applied`）。
3. 原子力発電所を設置し、災害(隕石/竜巻等)または火災で全焼/崩壊させる。
4. 崩壊地点に爆発エフェクトが出て、周囲約700mが汚染（紫/汚染色）になることを確認。
5. 汚染ゾーン近傍に下水処理施設を稼働 → 汚染が徐々に消えることを確認。
6. （時間確認）ゲーム内で50年経過 → 汚染が自動消滅することを確認。
7. セーブ→ロードで汚染が維持されることを確認。

## 設定
定数は `Game/ModConfig.cs`（汚染半径・除染猶予年数・除染施設キーワード等）。

## ログ
`%LOCALAPPDATA%\Colossal Order\Cities_Skylines\` の output_log で `[NuclearMeltdown]` を検索。
