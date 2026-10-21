# Third-Party Notices

Character Design Prompt Generator 使用了以下第三方素材。

## Microsoft Fluent Emoji（3D 圖像）

- 檔案：`Emoji\fluent-emoji-3d.zip`（3,145 張 PNG，縮放為 128×128）
- 來源：
  - https://github.com/shuding/fluentui-emoji-unicode （commit `7a40f1a2d064d76e436813edc0f09b6c8cde5da8`，依 Unicode 碼點重新命名的 Fluent Emoji，2,980 張）
  - https://github.com/microsoft/fluentui-emoji （commit `1ffb34c752ecf5d402f04cfb4b392c77f57c54bc`，補上前者缺少的 165 張，例如 Emoji 15 新增的 🪼）
- 修改：縮放為 128×128 PNG，並以「去除 FE0E/FE0F 的十六進位碼點序列」重新命名；未修改圖像內容。產生方式見 `tools\BuildEmojiPack.cs`。
- 授權：MIT License（全文同時附在 `Emoji\LICENSE-fluentui-emoji.txt` 及圖庫 zip 內的 `LICENSE.txt`）

```
MIT License

Copyright (c) Microsoft Corporation.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE
```
