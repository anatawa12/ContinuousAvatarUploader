# Continuous Avatar Uploader

複数のアバターをtagをつけながら連続的にアップロードするツール

## Planned Features

- [x] 複数のアバターを連続的にアップロードする
- [x] アバターの説明をバージョン名に合わせて更新する。例えば `(v1)` -> `(v2)` のような形で。
- [x] 設定可能な前置詞/後置詞をつけてgitのtag付を自動的に行う
- [ ] ~~Automatically switch target platform and branch depends on platform (e.g. for quest, `quest-master` and for PC, `master` branch.)~~

## インストール方法

1. [このリンク][VCC-add-repo-link]をクリックしてanatawa12のレポジトリを追加する。
2. VCCでContinuous Avatar Uploaderを追加する。

[VCC-add-repo-link]: https://vpm.anatawa12.com/add-repo

## 使い方

### 1 アバターを設定する

アップロードするアバターの設定を作ります。
複数のアバターをまとめて`Avatar Upload Setting Group`を作成することも、一つの`Avatar Upload Setting`で一つのファイルにすることもできます。

どちらを作成する場合もProjectウィンドウ内で右クリックから`Create/Avatar Upload Setting`または`Create/Avatar Upload Setting Group`を選択してください。

`Avatar Upload Setting`の場合はSet Avatarの欄にHierarchyからアバターをドラック&ドロップしてそのAvatar Upload Settingに紐付けてください。

`Avatar Upload Setting Group`の場合はAvatar to Addの欄にHierarchyからアバターをドラック&ドロップしてAdd Avatarを押すことでアバターをAvatar Upload Setting Groupに追加してください。

次にそれそれのアバターの設定を行います。これはどちらでも共通です。

<!-- override blueprintの設定欄の話は多分ここ -->

２つのチェックボックスはそれぞれアバターを各プラットフォーム向けビルドでビルドするかを選択します。
ここでチェックされていないアバターはアップロードされません。

それぞれのプラットフォームごとにContinuous Avatar Uploaderの補助機能をon/offできます。

#### Versioning System

アバターの説明欄にアバターのバージョン番号を付加する機能です。カッコの中に接頭辞付きでバージョン番号を入れる想定です。

#### git tagging

Versioning Systemが有効なとき、gitのtagをバージョン番号をもとに自動的に付ける機能です。
tagには自由な接頭辞/接尾辞を指定できます

### 2 アップロードする

1. `Window/Continuous Avatar Uploader`からContinuous Avatar Uploaderを開きます。
2. Avatar Upload SettingまたはAvatar Upload Setting Groupを指定します。Groupを指定した場合、そのグループの中のすべてのアバターがアップロードされます。
   Avatar Upload Setting Groupの中の各アバターをAvatar Upload Settingに指定することもできます。
3. Start Uploadをクリックします。
