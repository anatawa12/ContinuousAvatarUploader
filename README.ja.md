# Continuous Avatar Uploader

複数のアバターをtagをつけながら連続的にアップロードするツール

## Planned Features

- [x] 複数のアバターを連続的にアップロードする
- [x] アバターの説明をバージョン名に合わせて更新する。例えば `(v1)` -> `(v2)` のような形で。
- [x] 設定可能な前置詞/後置詞をつけてgitのtag付を自動的に行う
- [ ] ~~Automatically switch target platform and branch depends on platform (e.g. for quest, `quest-master` and for PC, `master` branch.)~~ (not planned)

## インストール方法

1. [このリンク][VCC-add-repo-link]をクリックしてanatawa12のレポジトリを追加する。
2. VCCでContinuous Avatar Uploaderを追加する。

[VCC-add-repo-link]: https://vpm.anatawa12.com/add-repo

## 使い方

### 1 アバターを設定する

アップロードするアバターの設定ファイルを作ります。
`Avatar Upload Setting Group`で複数のアバターの設定を一つのファイルに纏めるか、`Avatar Upload Setting`でアバターごとにファイルを別にすることができます。\

設定ファイルを作成するにはProjectウィンドウ内で右クリックし、`Create/Avatar Upload Setting`、または`Create/Avatar Upload Setting Group`を選択してください。

`Avatar Upload Setting`ファイルは、そのInspectorのSet Avatarの欄にアバターをHierarchyからドラッグ&ドロップすることでアバターと紐付けることが出来ます。

`Avatar Upload Setting Group`ファイルは、そのInspectorのAvatar to Addの欄にアバターをHierarchyからドラッグ&ドロップして、Add Avatarを押すことでアバターと紐付けることが出来ます。

次にそれそれのアバターの設定を行います。これはどちらのファイルでも共通です。

<!-- override blueprintの設定欄の話は多分ここ -->

二つのチェックボックスは、アバターを各プラットフォーム向けにビルドするかどうかの選択です。
チェックされていないプラットフォーム向けにはアバターのアップロードが行われません。

各プラットフォーム向けビルドについて、Continuous Avatar Uploaderの補助機能は個別にon/offすることが出来ます。

#### Versioning System

アバターの説明欄にアバターのバージョン番号を付加する機能です。カッコの中に接頭辞付きでバージョン番号を入れる想定です。

#### git tagging

Versioning Systemが有効なとき、gitのtagをバージョン番号をもとに自動的に付ける機能です。
tagには自由な接頭辞/接尾辞を指定できます

### 2 アップロードする

1. `Window/Continuous Avatar Uploader`からContinuous Avatar Uploaderを開きます。
2. Continuous Avatar Uploaderに、アップロードしたいアバターのAvatar Upload SettingまたはAvatar Upload Setting Groupを指定します。
   Groupを指定した場合、そのグループの中のすべてのアバターがアップロードされます。
   また、Avatar Upload Setting Groupの中のAvatar Upload Setting (Sub Assetになっています) を個別にAvatar Upload Setting欄に指定することもできます。
3. Start Uploadをクリックします。
