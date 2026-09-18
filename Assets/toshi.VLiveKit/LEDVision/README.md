# VLive LED Vision

LED スクリーン表現と LTCGI 連携を扱うための Unity / HDRP package です。

## Package

- Package name: `com.toshi.vlivekit.ledvision`
- Version: `0.1.4`
- Unity: 2022.3
- Repository: https://github.com/toshi-kundesu/VLiveKit_LEDVision
- Package root: `Assets/toshi.VLiveKit/LEDVision`

## 主な内容

- LED スクリーン向け shader / material
- 映像を光源として扱うための LTCGI 連携
- HDRP 環境でのライブステージ照明表現

## 依存・同梱 asset

- HDRP 14.0.8
- LTCGI: https://github.com/PiMaker/ltcgi

## インストール

Unity の `Packages/manifest.json` の `dependencies` に追加します。

```json
{
  "dependencies": {
    "com.toshi.vlivekit.ledvision": "https://github.com/toshi-kundesu/VLiveKit_LEDVision.git?path=/Assets/toshi.VLiveKit/LEDVision#main"
  }
}
```

VLiveKit sandbox では submodule として `Packages/VLiveKit_LEDVision` に配置し、`file:` 参照で読み込んでいます。

## LTCGI サンプル

`LTCGI/LTCGI_TestScene/Scenes/LTC_GI_Test.unity` を開いて再生すると、
同梱動画を LED スクリーンへ表示し、その映像を LTCGI の光源として確認できます。
テンプレートでは `toshi/VLiveKit/Samples` の案内から開けます。

Video Player は通常速度・音声ありのループ再生です。Unity 6000.4.6f1 で
映像停止や音声の乱れを起こす VideoPlayer の回帰不具合を回避するため、
`Skip On Drop` を無効にしています。フレーム落ちを追いかける設定へ戻す場合は、
修正版の Unity で確認してください。この不具合は 6000.4.11f1 で修正されています。
[Unity Issue Tracker](https://issuetracker.unity.com/issues/17199/audiosampleprovider-buffer-overflow-warnings-are-thrown-when-any-video-is-played-in-play-mode)

## 注意

- 同梱 third-party asset には個別の license が適用されます。

## License

この package 独自のコードと asset は repository の `LICENSE` に従います。third-party asset を含む場合は、それぞれの license / README を確認してください。
