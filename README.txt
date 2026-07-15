✨ SIDERAL : Sorcelleries, Incantations et Disciplines Ésotériques Réassignés Aléatoirement aux Loots 💫
======================================================================================================

Version actuelle / Current version / 当前版本 : V1.0.3
Version figée le / Frozen on / 固定日期 : 2026-07-14

Règle de versioning / Versioning rules / 版本规则 :
- Amélioration majeure / Major improvement / 重大改进 : +1 version majeure (V2.0.0, V3.0.0...)
- Amélioration mineure / Minor improvement / 小改进 : +0.1 version mineure (V1.1.0, V1.2.0, V2.1.0...)
- Correctif de bug / Bug fix / Bug 修复 : +0.0.1 version corrective (V1.0.1, V1.0.2, V2.0.1, V2.1.1...)

Languages in this file:
- Français
- English
- 中文


====================
FRANÇAIS
====================

✨ SIDERAL : Sorcelleries, Incantations et Disciplines Ésotériques Réassignés Aléatoirement aux Loots 💫

SIDERAL a été fortement pensé pour être compatible avec le mod Randomizer.
Il peut aussi être utilisé totalement indépendamment via le profil Autonome.

SIDERAL génère un regulation.bin modifié afin de transformer des armes en sorcelleries et incantations, selon les options choisies dans l'application.

Disclaimer / transparence : je m'excuse par avance s'il existe des fautes, erreurs ou interprétations incorrectes dans les traductions. Je me suis également aidé de l'IA pour certaines parties de l'application que je ne maîtrise pas encore, afin d'être transparent avec les joueurs.


1. Prérequis
------------

- Windows x64.
- Elden Ring installé.
- Mod Engine 2 n'est pas inclus dans l'archive SIDERAL.
- Pour le profil Autonome, téléchargez Mod Engine 2 depuis https://github.com/soulsmods/ModEngine2/releases, puis sélectionnez modengine2_launcher.exe si SIDERAL le demande.
- .NET 9 Desktop Runtime x64 peut être nécessaire si SIDERAL.exe ne se lance pas.
- Steam doit être ouvert pour le lancement via Mod Engine.
- Lancez le jeu hors ligne avec des fichiers modifiés. Ne jouez pas en ligne avec ce type de mod.


2. Comment utiliser le mod
--------------------------

1. Décompressez le dossier complet de SIDERAL. Ne lancez pas l'exe seul sans les DLL, les dossiers Data, Base et Defs.
2. Lancez SIDERAL.exe.
3. Choisissez la langue de l'application si besoin : Français / English / 中文.
4. Vérifiez les chemins :
   - Régulation source : le regulation.bin de base utilisé pour générer le mod.
   - Régulation générée : le fichier qui sera créé, par défaut Output\regulation.bin.
   - Exécutable du jeu : le fichier eldenring.exe, par exemple steamapps\common\ELDEN RING\Game\eldenring.exe. Comme le Randomizer, SIDERAL utilise ce chemin comme référence du jeu, puis lance via Mod Engine en interne.
   - Exécutable du Randomizer : le fichier EldenRingRandomizer.exe. Ce chemin sert au bouton "Lancer le mod Randomizer" avec le profil Compatible Randomizer.
5. Choisissez le profil :
   - Compatible Randomizer : à utiliser si vous combinez SIDERAL avec le mod Randomizer. Ce profil génère Output\regulation.bin pour le fusionner ensuite dans le Randomizer.
   - Autonome : à utiliser si vous utilisez SIDERAL seul, sans passer par le Randomizer.
6. Choisissez le type de magie :
   - Sorcelleries et incantations.
   - Sorcelleries uniquement.
   - Incantations uniquement.
7. Seed :
   - Laissez vide pour générer un nouveau Seed automatiquement.
   - Indiquez un nombre pour refaire exactement la même génération.
8. Cochez ou décochez les options voulues.
   - En profil Autonome, l'option "Retirer les prérequis de stats" peut être décochée si vous voulez conserver les prérequis vanilla des bâtons, sceaux, sorcelleries et incantations.
9. Cliquez sur Générer regulation.bin.
10. Une fois le fichier généré, vous avez deux possibilités :

   Option 1 - Lancer SIDERAL en Autonome via Mod Engine :
   - Utilisez le profil Autonome.
   - Renseignez le champ "Exécutable du jeu" avec le chemin vers eldenring.exe.
   - Cliquez sur "Lancer le jeu".
   - SIDERAL crée Output\config_sideral.toml et lance Mod Engine avec le dossier Output comme dossier de mod.
   - Le regulation.bin du jeu n'est pas remplacé directement.
   - Si l'option de transformation des textes est activée, Output\msg\... sera aussi chargé par Mod Engine depuis le dossier Output.
   - Au premier lancement, si SIDERAL ne trouve pas Mod Engine 2, sélectionnez manuellement modengine2_launcher.exe.
   - Vous pouvez aussi placer le dossier ModEngine téléchargé à côté de SIDERAL.exe.

   Option 2 - Fusionner avec le mod Randomizer :
   - Utilisez le profil Compatible Randomizer.
   - Lancez le mod Randomizer, ou cliquez sur "Lancer le mod Randomizer" si le chemin de EldenRingRandomizer.exe est renseigné dans SIDERAL.
   - Cliquez sur "Fusionner avec un autre mod".
   - Choisissez l'option "Sélectionner le regulation.bin à fusionner".
   - Renseignez le chemin du regulation.bin généré par SIDERAL : Output\regulation.bin.

11. Lancez Elden Ring via votre méthode habituelle.


3. Remarques importantes
------------------------

- Si l'option "Armes de départ des classes -> Magie" est cochée, ne randomisez pas les armes de départ des classes via le mod Randomizer. Sinon la génération du Seed peut échouer.
- L'option "Boucliers -> Bâtons / Sceaux" transforme les boucliers en bâtons et sceaux. Il est possible d'effectuer des parades avec les sceaux et les bâtons.
- Si un autre mod modifie aussi regulation.bin ou les fichiers msgbnd, il peut y avoir conflit. Avec Mod Engine 2, l'ordre des dossiers de mod détermine quel fichier est utilisé.
- Si une arme physique apparaît encore en jeu alors qu'elle devrait être transformée, vérifiez que le dernier Output\regulation.bin généré est bien celui chargé par Mod Engine.
- Le champ "Exécutable du jeu" n'est pas vérifié automatiquement avant le lancement. L'utilisateur doit renseigner le bon chemin vers eldenring.exe.
- Pour lancer en Autonome, SIDERAL doit trouver le modengine2_launcher.exe que vous avez téléchargé séparément.
- Si SIDERAL ne trouve pas modengine2_launcher.exe automatiquement, une fenêtre de sélection s'ouvrira pour le choisir manuellement.
- Gardez toujours une sauvegarde de vos fichiers de base avant de remplacer des fichiers dans un dossier de mod.


4. Infos supplémentaires
------------------------

- Les logs et les fichiers de mapping sont créés dans le dossier Logs. Ils permettent de retrouver quels objets ont été transformés.
- Le même Seed avec les mêmes options doit produire la même génération.
- Les textes transformés sont générés dans Output\msg lorsque les fichiers de langue correspondants sont disponibles.
- Le profil "Compatible Randomizer" est pensé pour fonctionner avec une installation ou un regulation.bin préparé pour Randomizer.
- Le profil "Autonome" est pensé pour utiliser SIDERAL sans Randomizer.
- Pour les joueurs UWYG : je ne garantis pas pour le moment le fait d'obtenir systématiquement une sorcellerie / une incantation offensive. Je vais améliorer cela une fois que la version "Tarnished Edition" sera sortie, donc pas avant septembre.


5. Améliorer le mod / proposer des retours
------------------------------------------

Pour proposer une amélioration, signaler un problème ou envoyer une idée :

- Envoyez-moi un message directement sur NexusMod.
- Lien direct vers la messagerie NexusMods : https://forums.nexusmods.com/messenger/
- Dans le champ "to", mettez : gardial
- Ajoutez également : [Mod SIDERAL]

Cela me permet de retrouver plus facilement les messages concernant ce mod.


====================
ENGLISH
====================

✨ SIDERAL: Sorceries, Incantations, and Esoteric Disciplines Randomly Reassigned to Loots 💫

SIDERAL was strongly designed to be compatible with the Randomizer mod.
It can also be used completely independently through the Standalone profile.

SIDERAL generates a modified regulation.bin that transforms weapons into sorceries and incantations, depending on the options selected in the application.

Disclaimer / transparency: I apologize in advance if there are mistakes, errors, or incorrect interpretations in the translations. I also used AI assistance for some parts of the application that I do not fully master yet, in order to be transparent with players.


1. Requirements
---------------

- Windows x64.
- Elden Ring installed.
- Mod Engine 2 is not included in the SIDERAL archive.
- For the Standalone profile, download Mod Engine 2 from https://github.com/soulsmods/ModEngine2/releases, then select modengine2_launcher.exe if SIDERAL asks for it.
- .NET 9 Desktop Runtime x64 may be required if SIDERAL.exe does not start.
- Steam must be open when launching through Mod Engine.
- Launch the game offline when using modified files. Do not play online with this type of mod.


2. How to use the mod
---------------------

1. Extract the full SIDERAL folder. Do not run the exe alone without the DLLs and the Data, Base and Defs folders.
2. Launch SIDERAL.exe.
3. Select the application language if needed: Francais / English / 中文.
4. Check the paths:
   - Source regulation: the base regulation.bin used to generate the mod.
   - Generated regulation: the file that will be created, by default Output\regulation.bin.
   - Game executable: eldenring.exe, for example steamapps\common\ELDEN RING\Game\eldenring.exe. Like the Randomizer, SIDERAL uses this path as the game reference, then launches through Mod Engine internally.
   - Randomizer executable: EldenRingRandomizer.exe. This path is used by the "Launch Randomizer mod" button with the Randomizer Compatible profile.
5. Choose the profile:
   - Randomizer Compatible: use this if you combine SIDERAL with the Randomizer mod. This profile generates Output\regulation.bin so it can be merged in the Randomizer.
   - Standalone: use this if you use SIDERAL by itself, without the Randomizer.
6. Choose the magic type:
   - Sorceries and incantations.
   - Sorceries only.
   - Incantations only.
7. Seed:
   - Leave empty to generate a new Seed automatically.
   - Enter a number to reproduce the exact same generation.
8. Enable or disable the options you want.
   - In Standalone, "Remove stat requirements" can be disabled if you want to keep the vanilla requirements for staffs, seals, sorceries, and incantations.
9. Click Generate regulation.bin.
10. Once the file has been generated, you have two options:

   Option 1 - Launch SIDERAL in Standalone through Mod Engine:
   - Use the Standalone profile.
   - Fill the "Game executable" field with the path to eldenring.exe.
   - Click "Launch game".
   - SIDERAL creates Output\config_sideral.toml and launches Mod Engine with the Output folder as the mod folder.
   - The game's regulation.bin is not directly replaced.
   - If text transformation is enabled, Output\msg\... will also be loaded by Mod Engine from the Output folder.
   - On first launch, if SIDERAL cannot find Mod Engine 2, manually select modengine2_launcher.exe.
   - You can also place the downloaded ModEngine folder next to SIDERAL.exe.

   Option 2 - Merge with the Randomizer mod:
   - Use the Randomizer Compatible profile.
   - Launch the Randomizer mod, or click "Launch Randomizer mod" if the EldenRingRandomizer.exe path is set in SIDERAL.
   - Click "Merge other mod".
   - Choose the option to select the regulation.bin to merge.
   - Select the regulation.bin generated by SIDERAL: Output\regulation.bin.

11. Launch Elden Ring using your usual method.


3. Important notes
------------------

- If "Starting class weapons -> Magic" is enabled, do not randomize starting class weapons through the Randomizer mod. Otherwise Seed generation may fail.
- "Shields -> Staffs / Seals" transforms shields into staffs and seals. Seals and staffs can parry.
- If another mod also changes regulation.bin or msgbnd files, conflicts may happen. With Mod Engine 2, the order of mod folders decides which file is used.
- If a physical weapon still appears in game when it should be transformed, check that the latest generated Output\regulation.bin is the one loaded by Mod Engine.
- The "Game executable" field is not automatically verified before launching. The user must enter the correct path to eldenring.exe.
- To launch in Standalone, SIDERAL must find the modengine2_launcher.exe file you downloaded separately.
- If SIDERAL cannot find modengine2_launcher.exe automatically, a file selection window will open so you can choose it manually.
- Always keep a backup of your base files before replacing files in a mod folder.


4. Additional information
-------------------------

- Logs and mapping files are created in the Logs folder. They let you see which items were transformed.
- The same Seed with the same options should produce the same generation.
- Transformed texts are generated in Output\msg when the corresponding language files are available.
- The "Randomizer Compatible" profile is designed to work with an installation or regulation.bin prepared for Randomizer.
- The "Standalone" profile is designed to use SIDERAL without Randomizer.
- For UWYG players: I do not currently guarantee that you will always get an offensive sorcery / incantation. I will improve this once the "Tarnished Edition" version is released, so not before September.


5. Improve the mod / send feedback
----------------------------------

To suggest an improvement, report a problem, or send an idea:

- Send me a direct message on NexusMods.
- Direct link to NexusMods messages: https://forums.nexusmods.com/messenger/
- In the "to" field, enter: gardial
- Also add: [Mod SIDERAL]

This helps me find messages related to this mod more easily.


====================
中文
====================

✨ SIDERAL：魔法、祷告与秘术，随机重分配至战利品 💫

SIDERAL 的设计重点之一是兼容 Randomizer 模组。
它也可以通过独立模式完全单独使用。

SIDERAL 会生成一个修改后的 regulation.bin，并根据你在程序中选择的选项，将武器转换为魔法和祷告。

免责声明 / 透明说明：如果翻译中存在错字、错误或理解不准确的地方，我提前表示歉意。为了对玩家保持透明，我也在自己尚未完全掌握的部分应用程序内容上使用了 AI 辅助。


1. 需求
-------

- Windows x64。
- 已安装 Elden Ring。
- SIDERAL 压缩包不包含 Mod Engine 2。
- 如需使用独立模式，请从 https://github.com/soulsmods/ModEngine2/releases 下载 Mod Engine 2；如果 SIDERAL 要求选择文件，请选择 modengine2_launcher.exe。
- 如果 SIDERAL.exe 无法启动，可能需要安装 .NET 9 Desktop Runtime x64。
- 通过 Mod Engine 启动时，Steam 必须保持打开。
- 使用修改文件时请离线启动游戏。不要使用这类模组进行在线游戏。


2. 如何使用
-----------

1. 解压完整的 SIDERAL 文件夹。不要只单独运行 exe，必须保留 DLL 以及 Data、Base 和 Defs 文件夹。
2. 启动 SIDERAL.exe。
3. 如有需要，在程序中选择语言：Francais / English / 中文。
4. 检查路径：
   - 源 regulation：用于生成模组的基础 regulation.bin。
   - 生成的 regulation：将被创建的文件，默认是 Output\regulation.bin。
   - 游戏可执行文件：eldenring.exe，例如 steamapps\common\ELDEN RING\Game\eldenring.exe。和 Randomizer 一样，SIDERAL 使用这个路径作为游戏位置参考，然后在内部通过 Mod Engine 启动。
   - Randomizer 可执行文件：EldenRingRandomizer.exe。该路径用于兼容 Randomizer 配置下的“启动 Randomizer 模组”按钮。
5. 选择配置：
   - 兼容 Randomizer：如果你要将 SIDERAL 与 Randomizer 模组一起使用，请选择这个配置。它会生成 Output\regulation.bin，之后可以在 Randomizer 中合并。
   - 独立模式：如果你只使用 SIDERAL，不通过 Randomizer，请选择这个配置。
6. 选择魔法类型：
   - 魔法与祷告。
   - 仅魔法。
   - 仅祷告。
7. Seed：
   - 留空会自动生成新的 Seed。
   - 输入数字可以复现完全相同的生成结果。
8. 勾选或取消你想使用的选项。
   - 在独立模式中，可以取消勾选“移除属性需求”，以保留法杖、圣印、魔法和祷告的原始属性需求。
9. 点击“生成 regulation.bin”。
10. 文件生成后，你有两种使用方式：

   方式 1 - 通过 Mod Engine 以独立模式启动 SIDERAL：
   - 使用独立模式配置。
   - 在“游戏可执行文件”字段中填写 eldenring.exe 的路径。
   - 点击“启动游戏”。
   - SIDERAL 会创建 Output\config_sideral.toml，并使用 Output 文件夹作为模组文件夹启动 Mod Engine。
   - 游戏本体的 regulation.bin 不会被直接替换。
   - 如果启用了文本转换，Output\msg\... 也会由 Mod Engine 从 Output 文件夹加载。
   - 第一次启动时，如果 SIDERAL 找不到 Mod Engine 2，请手动选择 modengine2_launcher.exe。
   - 你也可以把下载好的 ModEngine 文件夹放在 SIDERAL.exe 旁边。

   方式 2 - 与 Randomizer 模组合并：
   - 使用兼容 Randomizer 配置。
   - 启动 Randomizer 模组；如果已在 SIDERAL 中填写 EldenRingRandomizer.exe 路径，也可以点击“启动 Randomizer 模组”。
   - 点击合并其他模组的选项。
   - 选择用于合并的 regulation.bin。
   - 选择由 SIDERAL 生成的 regulation.bin：Output\regulation.bin。

11. 使用你平时的方法启动 Elden Ring。


3. 重要说明
-----------

- 如果启用了“初始职业武器 -> 魔法”，不要在 Randomizer 模组中随机化初始职业武器，否则 Seed 生成可能失败。
- “盾牌 -> 法杖 / 圣印”会将盾牌转换为法杖和圣印。圣印和法杖可以进行弹反。
- 如果其他模组也修改 regulation.bin 或 msgbnd 文件，可能会产生冲突。使用 Mod Engine 2 时，模组文件夹的顺序会决定最终加载哪个文件。
- 如果游戏中仍然出现本应被转换的实体武器，请确认 Mod Engine 加载的是最新生成的 Output\regulation.bin。
- “游戏可执行文件”字段在启动前不会自动验证。用户需要自行填写正确的 eldenring.exe 路径。
- 要使用独立模式启动，SIDERAL 需要找到你单独下载的 modengine2_launcher.exe。
- 如果 SIDERAL 无法自动找到 modengine2_launcher.exe，会打开一个文件选择窗口，让你手动选择。
- 在替换模组文件夹中的文件前，请始终保留基础文件备份。


4. 其他信息
-----------

- 日志和映射文件会创建在 Logs 文件夹中，可用于查看哪些物品被转换。
- 相同的 Seed 和相同的选项应当生成相同结果。
- 当对应语言文件可用时，转换后的文本会生成在 Output\msg 中。
- “兼容 Randomizer”配置用于配合 Randomizer 准备的安装或 regulation.bin。
- “独立模式”配置用于不通过 Randomizer 单独使用 SIDERAL。
- 对 UWYG 玩家：目前我不能保证一定会获得攻击型魔法 / 祷告。我会在“Tarnished Edition”版本发布后改进这一点，因此不会早于九月。


5. 改进模组 / 提供反馈
-----------------------

如果你想提出改进建议、报告问题，或发送想法：

- 请在 NexusMods 上给我发送私信。
- NexusMods 私信页面：https://forums.nexusmods.com/messenger/
- 在 "to" 字段中填写：gardial
- 同时添加：[Mod SIDERAL]

这样我可以更容易找到与本模组相关的消息。
