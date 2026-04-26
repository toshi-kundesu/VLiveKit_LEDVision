## 概要

VLiveKitの一部として開発している、  
LED表現および間接光連携のためのシェーダーパッケージです。

LEDスクリーン表現と、LTCGIを用いたライティング連携を  
HDRP環境で扱うことを目的としています。

---

## 主な機能

### LED表現

- LEDスクリーン用シェーダー
- 映像を前提とした発光表現

---

### LTCGI連携（HDRP対応）

- LTCGIのHDRP対応実装
- LTCGIを受信するためのShaderGraph
- カスタムノードの提供

シーン内のオブジェクトが、LED映像などの間接光を受ける表現を可能にします。

---

## 含まれるライブラリ

本パッケージには以下のライブラリをベースとした実装が含まれています：

- LTCGI  
  https://github.com/PiMaker/ltcgi  
  License: MIT

※ 上記ライブラリには個別のライセンスが適用されます。

---

## 開発状況

本パッケージはライブ制作での使用を前提に、  
継続的に調整・改善を行っています。

---

## インストール

`Packages/manifest.json` の `dependencies` に以下を追加してください。

```json
{
  "dependencies": {
    "com.toshi.vlivekit.ledvision": "https://github.com/toshi-kundesu/VLiveKit_LEDVision.git?path=/Assets/toshi.VLiveKit/LEDVision#main"
  }
}
