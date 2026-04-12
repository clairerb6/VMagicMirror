# URP移行タスク

## 現状整理

- Unity プロジェクト全体としては `ProjectSettings/GraphicsSettings.asset` で URP が有効になっている。
- ただし品質設定ごとの `customRenderPipeline` は全て未設定で、品質切り替え時に URP Asset の切り替え基盤が未整備。
- メインカメラ系 prefab の `UniversalAdditionalCameraData` では `m_RenderPostProcessing: 0` のままになっている。
- 旧 Post Processing Stack 前提のコードはかなりの範囲でコメントアウトされており、機能が「壊れている」のではなく「未移植で停止中」のものが多い。
- 影用シェーダは Built-in 版 `ShadowDrawer.shader` と URP 版 `UrpShadowDrawer.shader` の両方があるので、完全に未着手ではない。
- GLB アクセサリー / Buddy GLB 読み込みは `BuiltInVrm10MaterialDescriptorGenerator` を使っており、URP 用マテリアル生成に未対応。

## 優先順位

1. 描画基盤の固定
2. アバター描画の見た目復旧
3. ポスプロ・透過系の再設計
4. 補助機能の復旧
5. 動作確認シナリオの整備

## タスク一覧

### P0: URP の土台を固定する

- `ProjectSettings/QualitySettings.asset` に品質ごとの `customRenderPipeline` を設定する
  - 今は全品質で `{fileID: 0}` のまま。
  - まずは全品質で同じ `Assets/Baku/VMagicMirror/GraphicsSetting/URPAsset.asset` を刺して動作安定を優先し、その後に品質別 asset 分岐をやる。
- `Assets/Baku/VMagicMirror/GraphicsSetting/URPAsset.asset` の品質責務を決める
  - 現状は単一 asset で、MSAA/Shadow/AO/ポスプロなどの品質差分を表現できていない。
  - 「単一 asset + スクリプト制御」で行くか、「品質別 URPAsset 複数」で行くかを最初に決める。
- メインカメラ prefab 群の URP カメラ設定を点検する
  - `Assets/Baku/VMagicMirror/Prefabs/Camera/Main Camera.prefab`
  - `Assets/Baku/VMagicMirror/Prefabs/TextureSharing/SpoutSender.prefab`
  - いずれも `m_RenderPostProcessing: 0`、`m_Antialiasing: 0` のため、コードを直しても効かない可能性が高い。
- Volume の置き場を決める
  - Global Volume をシーン常駐にするか、カメラ prefab にぶら下げるかを統一する。
  - `DefaultVolumeProfile.asset` と既存の `MainViewer_Profiles/PostProcessing Profile.asset` のどちらを正とするかも決める。

### P1: アバター表示の最低限を復旧する

- MToon の見た目確認を最優先でやる
  - シェードが「出ていない」のか「ライト強度が変わって見えるだけ」なのかを切り分ける。
  - `Assets/Baku/VMagicMirror/Scripts/Environment/CameraAndLights/LightingController.cs` に VRM10 化後の見た目補正コメントがあり、今回の URP 差分と混ざっている可能性がある。
- アウトライン不具合の原因切り分け
  - VRM/UniVRM 側の URP MToon がアウトライン対応済みか
  - renderer feature や depth / normal texture 要求が不足していないか
  - カメラ側設定不足か
  - まずは既存 VRM アバター 1 体で「Built-in と URP の見た目差」をスクショ比較して確認する。
- `ShadowDrawer` の適用経路を確認して URP 版に寄せる
  - `Assets/Baku/VMagicMirror/Shaders/UrpShadowDrawer.shader` はあるが、実際にどこで割り当てているかの確認が必要。
  - `Custom/ShadowDrawer` を直接参照している prefab / material / script を洗い出し、URP 実行時に `Custom/UrpShadowDrawer` を使うように統一する。
- `ShadowBoardMotion` と固定影の表示確認
  - メモにある「陰 (ShadowDrawer)」が単なるシェーダ差し替え不足なのか、描画順 / light / layer の問題かを確認する。
  - ここは P0 のカメラ設定が固まってから再確認する。

### P1.5: アクセサリー / サブキャラのマテリアル経路を URP 対応にする

- GLB アクセサリー読込を URP 用 material generator に切り替える
  - `Assets/Baku/VMagicMirror/Scripts/Accessory/AccessoryFileReader.cs`
  - 現状は `BuiltInVrm10MaterialDescriptorGenerator` を使用している。
- Buddy GLB 読込も同様に URP 対応にする
  - `Assets/Baku/VMagicMirror/Scripts/Buddy/Instance/BuddyGlbInstance.cs`
- 移行後に確認する項目
  - MToon を含む GLB アクセサリー
  - 非 MToon GLB
  - サブキャラのマテリアル割当
  - 既存アクセサリー prefab の Standard / custom shader が URP でも破綻しないか

### P2: ポスプロ機能を URP Volume ベースで再実装する

- 旧 PPSv2 依存コードを URP Volume に移植する
  - `Assets/Baku/VMagicMirror/Scripts/Environment/CropAndOutlineController.cs`
  - `Assets/Baku/VMagicMirror/Scripts/Environment/CameraAndLights/AntiAliasSettingSetter.cs`
  - `Assets/Baku/VMagicMirror/Scripts/Environment/CameraAndLights/LightingController.cs`
  - 現状はコメントアウトで実質停止中。
- Bloom 制御を URP Volume に移植
  - `LightingController` の Bloom コマンド受信は残っているが、適用先がない。
- Ambient Occlusion 制御を URP Volume / RendererFeature に移植
  - Renderer 側には SSAO feature があるが、コードから enable/intensity/color を変更する経路がない。
  - URP の SSAO は旧 AO とパラメータ体系が違うので、完全一致ではなく「近い見た目に寄せる」方針で良い。
- アンチエイリアス設定を URP カメラ設定へ移植
  - `AntiAliasSettingSetter.cs` の旧 SMAA 制御を、`UniversalAdditionalCameraData.antialiasing` と `antialiasingQuality` へ置き換える。
  - MSAA は URPAsset 側設定なので、カメラ側 AA とどう住み分けるかを決める。

### P2.5: 透過・切り抜き・輪郭強調を再設計する

- `VmmCrop` / `VmmAlphaEdge` は旧 PPSv2 実装なので、URP ではそのまま動かない
  - `Assets/Baku/VMagicMirror/Scripts/Environment/PostProcessing/VmmCrop.cs`
  - `Assets/Baku/VMagicMirror/Scripts/Environment/PostProcessing/VmmAlphaEdge.cs`
  - どちらも現状コメントアウト済み。
- ここは「Volume Override でやる」のではなく、URP Renderer Feature / Full Screen Pass に寄せるのが本命
  - 切り抜き
  - 透過時の輪郭強調
  - VmmCrop に依存するクリック判定との整合
- 透過出力とスクショ機能はこのタスクの後で確認する
  - 現状、切り抜きが死んでいるので透過系確認を先にやってもノイズが多い。

### P3: デスクトップ色調チェックと周辺機能の復旧

- `uDesktopDuplication` クラッシュを切り分ける
  - `Assets/Baku/VMagicMirror/Scripts/Environment/DesktopLight/DesktopLightEstimator.cs`
  - `Graphics.Blit` + ComputeShader + 外部テクスチャの組み合わせが URP/Unity 6.3 で不安定になっている可能性がある。
  - まずは `ddTexture.monitor.texture` の取得可否、format、生成タイミングをログで確認。
- `LightingController.SetMainLightColor()` が `return;` で止まっている理由を整理する
  - Desktop Light を戻すなら、ここも復帰が必要。
  - 逆に使わないなら、機能廃止も含めて整理した方がよい。
- Spout 出力の確認
  - `Assets/Baku/VMagicMirror/Prefabs/TextureSharing/SpoutSender.prefab` でも post processing が無効。
  - 「Spout に載せたい絵」が main camera と同じなのか、透過版が必要なのかを先に決める。
- スクリーンショット機能の確認
  - ポスプロ無効・透過無効の影響を受けるので、P2 完了後にまとめて確認。

### P4: 未確認項目の回帰チェック

- キーボード / マウス等のデバイスオブジェクト表示
- アクセサリー表示
- サブキャラ表示
- Spout 出力
- 透過出力
- 切り抜き機能
- スクリーンショット
- 画質設定切替
- AA 切替
- SSAO 切替

## 着手順の提案

1. `QualitySettings` とカメラ prefab の URP 設定を先に固定する
2. 単一の検証シーン / 検証 VRM で MToon・アウトライン・影を確認する
3. GLB / Buddy の material generator を URP 化する
4. Bloom / AO / AA を URP Volume / CameraData に移植する
5. 透過・切り抜き・輪郭強調を Renderer Feature ベースで再実装する
6. DesktopLight / Spout / スクショを順に戻す
7. 最後に品質設定ごとの差分調整を行う

## 最初の1〜2日でやるとよさそうな具体作業

- `QualitySettings.asset` に URPAsset を設定
- `Main Camera.prefab` と `SpoutSender.prefab` の post processing を有効化
- 検証用に Global Volume を 1 つに固定
- URP での VRM1体表示を基準ケースにして、アウトライン / 影 / シェードの差分を確認
- `AccessoryFileReader.cs` と `BuddyGlbInstance.cs` の Built-in material generator を URP 対応に差し替える方針を決める

## 補足メモ

- 今回の不具合は「URP へ切り替えたら一部が壊れた」というより、「Built-in 前提の機能群がまだ URP の責務へ移されていない」が本質。
- そのため、個別バグ修正から入るより、まず
  - RenderPipeline / Quality
  - Camera / Volume
  - MaterialGenerator
  - Full Screen Effect 実装方式
  の4点を固定した方が全体の手戻りが少ない。
