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

設定ファイルを作成するにはProjectウィンドウ内で右クリックし、`Create/Avatar Upload Setting`、または`Create/Avatar Upload Setting Group`を選択し、Inspectorで設定を行います。

`Avatar Upload Setting`ファイルは、そのInspectorのSet Avatarの欄にアバターをHierarchyからドラッグ&ドロップすることでアバターと紐付けることが出来ます。

`Avatar Upload Setting Group`ファイルは、そのInspectorのAvatar to Addの欄にアバターをHierarchyからドラッグ&ドロップして、Add Avatarを押すことでアバターと紐付けることが出来ます。

また、0.3.0からは以下の作成方法が追加されました。
- ヒエラルキー上でアバターを右クリックし、`Continuous Avatar Uploader/Group from Selection`を選択することで、選択したアバターを含む`Avatar Upload Setting Group`を作成することができます。
- Project欄でアバターのPrefabを選択し、右クリックして`Create/Continuous Avatar Uploader/Group from Selection`を選択することで、選択したアバターを含む`Avatar Upload Setting Group`を作成することができます。
- Project欄でアバターのPrefabを選択し、右クリックして`Create/Continuous Avatar Uploader/Group of Variants from Prefab`を選択することで、選択したプレハブの子孫のPrefab Variantを含む`Avatar Upload Setting Group`を作成することができます。

次にそれそれのアバターの設定を行います。これはどちらのファイルでも共通です。

<!-- override blueprintの設定欄の話は多分ここ -->

二つのチェックボックスは、アバターを各プラットフォーム向けにビルドするかどうかの選択です。
チェックされていないプラットフォーム向けにはアバターのアップロードが行われません。

各プラットフォーム向けビルドについて、Continuous Avatar Uploaderの補助機能は個別にon/offすることが出来ます。

> **Note**
>
> Windows 環境においては、[VRChat SDKのバグにより](https://feedback.vrchat.com/sdk-bug-reports/p/uploading-avatar-may-freeze-when-antivirus-software-holds-handle-for-lastly-uplo)、ウイルス対策ソフトのリアルタイムスキャンが原因でアップロードがフリーズすることがあります。
> もしフリーズすることがあれば、以下のいずれかの対処を行うことができると思われます。
> - macOS か linux 環境に移行する
> - リアルタイムスキャンが終わるまで十分待つよう、Sleep Seconds を大きくする。
> - 自己責任で `%LOCALAPPDATA%\Temp\CompanyName\ProductName\` をリアルタイムスキャンの除外フォルダに追加する。セキュリティホールになるので気をつけてください。

#### Versioning System

アバターの説明欄にアバターのバージョン番号を付加する機能です。カッコの中に接頭辞付きでバージョン番号を入れる想定です。

#### git tagging

Versioning Systemが有効なとき、gitのtagをバージョン番号をもとに自動的に付ける機能です。
tagには自由な接頭辞/接尾辞を指定できます

### 2 アップロードする

1. `Window/Continuous Avatar Uploader`からContinuous Avatar Uploaderを開きます。
2. Continuous Avatar UploaderのSettings or Groupsに、アップロードしたいアバターのAvatar Upload SettingまたはAvatar Upload Setting Groupを指定します。
   Groupを指定した場合、そのグループの中のすべてのアバターがアップロードされます。
   また、Avatar Upload Setting Groupの中のAvatar Upload SettingはSub Assetになっているため、それらを個別に指定することもできます。
3. Start Uploadをクリックします。
