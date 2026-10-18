#define MyAppName "WallpaperSwitch"
#define MyAppVersion "1.3.1"
#define MyAppPublisher "WallpaperSwitch"
#define MyAppExeName "WallpaperSwitch.exe"
#define MyAppId "{{8F3A2B91-6D4E-4C17-A9B8-E1F0C4D7A256}"
#ifndef PublishDir
  #define PublishDir "..\artifacts\publish-1.3.1"
#endif

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\WallpaperSwitch
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\artifacts
OutputBaseFilename=RiyeQiehuan-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no
MinVersion=10.0.17763

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "arabic"; MessagesFile: "compiler:Languages\Arabic.isl"
Name: "armenian"; MessagesFile: "compiler:Languages\Armenian.isl"
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
Name: "bulgarian"; MessagesFile: "compiler:Languages\Bulgarian.isl"
Name: "catalan"; MessagesFile: "compiler:Languages\Catalan.isl"
Name: "corsican"; MessagesFile: "compiler:Languages\Corsican.isl"
Name: "czech"; MessagesFile: "compiler:Languages\Czech.isl"
Name: "danish"; MessagesFile: "compiler:Languages\Danish.isl"
Name: "dutch"; MessagesFile: "compiler:Languages\Dutch.isl"
Name: "finnish"; MessagesFile: "compiler:Languages\Finnish.isl"
Name: "french"; MessagesFile: "compiler:Languages\French.isl"
Name: "german"; MessagesFile: "compiler:Languages\German.isl"
Name: "hebrew"; MessagesFile: "compiler:Languages\Hebrew.isl"
Name: "hungarian"; MessagesFile: "compiler:Languages\Hungarian.isl"
Name: "italian"; MessagesFile: "compiler:Languages\Italian.isl"
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"
Name: "norwegian"; MessagesFile: "compiler:Languages\Norwegian.isl"
Name: "polish"; MessagesFile: "compiler:Languages\Polish.isl"
Name: "portuguese"; MessagesFile: "compiler:Languages\Portuguese.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "slovak"; MessagesFile: "compiler:Languages\Slovak.isl"
Name: "slovenian"; MessagesFile: "compiler:Languages\Slovenian.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "swedish"; MessagesFile: "compiler:Languages\Swedish.isl"
Name: "tamil"; MessagesFile: "compiler:Languages\Tamil.isl"
Name: "thai"; MessagesFile: "compiler:Languages\Thai.isl"
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"
Name: "ukrainian"; MessagesFile: "compiler:Languages\Ukrainian.isl"
Name: "chinesesimplified"; MessagesFile: "Languages\ChineseSimplified.isl"
Name: "chinesetraditional"; MessagesFile: "Languages\ChineseTraditional.isl"

[CustomMessages]
english.AdditionalTasks=Additional tasks:
english.StartupTaskDescription=Start WallpaperSwitch when you sign in to Windows (standard priority; enable high priority in the app)
english.ServiceRemovalError=Unable to remove the high-priority startup service. Uninstall was canceled. Approve the administrator prompt and try again.
arabic.AdditionalTasks=مهام إضافية:
arabic.StartupTaskDescription=تشغيل WallpaperSwitch عند تسجيل الدخول إلى Windows (بالأولوية العادية؛ فعّل الأولوية العالية في التطبيق)
arabic.ServiceRemovalError=تعذّرت إزالة خدمة بدء التشغيل ذات الأولوية العالية. أُلغيت عملية إلغاء التثبيت. وافق على طلب المسؤول ثم حاول مرة أخرى.
armenian.AdditionalTasks=Լրացուցիչ առաջադրանքներ.
armenian.StartupTaskDescription=Մուտք գործելիս գործարկել WallpaperSwitch-ը (ստանդարտ առաջնահերթությամբ․ բարձր առաջնահերթությունը միացրեք հավելվածում)
armenian.ServiceRemovalError=Չհաջողվեց հեռացնել բարձր առաջնահերթությամբ գործարկման ծառայությունը։ Ապատեղադրումը չեղարկվեց։ Հաստատեք ադմինիստրատորի հարցումը և կրկին փորձեք։
brazilianportuguese.AdditionalTasks=Tarefas adicionais:
brazilianportuguese.StartupTaskDescription=Iniciar o WallpaperSwitch ao entrar no Windows (prioridade padrão; ative a prioridade alta no aplicativo)
brazilianportuguese.ServiceRemovalError=Não foi possível remover o serviço de inicialização de alta prioridade. A desinstalação foi cancelada. Aprove a solicitação do administrador e tente novamente.
bulgarian.AdditionalTasks=Допълнителни задачи:
bulgarian.StartupTaskDescription=Стартиране на WallpaperSwitch при влизане в Windows (стандартен приоритет; включете висок приоритет в приложението)
bulgarian.ServiceRemovalError=Услугата за стартиране с висок приоритет не може да бъде премахната. Деинсталирането е отменено. Потвърдете заявката за администраторски права и опитайте отново.
catalan.AdditionalTasks=Tasques addicionals:
catalan.StartupTaskDescription=Inicia WallpaperSwitch en iniciar la sessió al Windows (prioritat estàndard; activa la prioritat alta a l'aplicació)
catalan.ServiceRemovalError=No s'ha pogut suprimir el servei d'inici d'alta prioritat. S'ha cancel·lat la desinstal·lació. Aprova la sol·licitud de l'administrador i torna-ho a provar.
corsican.AdditionalTasks=Attività supplementarie:
corsican.StartupTaskDescription=Avvia WallpaperSwitch quandu accedi à Windows (priorità standard; attiva a priorità alta in l'applicazione)
corsican.ServiceRemovalError=Ùn hè statu pussibule di caccià u serviziu d'avviu à priorità alta. A disinstallazione hè stata annullata. Accetta a dumanda d'amministratore è prova torna.
czech.AdditionalTasks=Další úkoly:
czech.StartupTaskDescription=Spouštět WallpaperSwitch při přihlášení do Windows (standardní priorita; vysokou prioritu zapněte v aplikaci)
czech.ServiceRemovalError=Službu spouštění s vysokou prioritou se nepodařilo odebrat. Odinstalace byla zrušena. Potvrďte žádost o oprávnění správce a zkuste to znovu.
danish.AdditionalTasks=Yderligere opgaver:
danish.StartupTaskDescription=Start WallpaperSwitch, når du logger på Windows (standardprioritet; aktivér høj prioritet i appen)
danish.ServiceRemovalError=Starttjenesten med høj prioritet kunne ikke fjernes. Afinstallationen blev annulleret. Godkend administratoranmodningen, og prøv igen.
dutch.AdditionalTasks=Extra taken:
dutch.StartupTaskDescription=WallpaperSwitch starten wanneer u zich aanmeldt bij Windows (standaardprioriteit; schakel hoge prioriteit in de app in)
dutch.ServiceRemovalError=De opstartservice met hoge prioriteit kan niet worden verwijderd. Het verwijderen van het programma is geannuleerd. Keur de beheerdersaanvraag goed en probeer het opnieuw.
finnish.AdditionalTasks=Lisätehtävät:
finnish.StartupTaskDescription=Käynnistä WallpaperSwitch, kun kirjaudut Windowsiin (tavallinen prioriteetti; ota suuri prioriteetti käyttöön sovelluksessa)
finnish.ServiceRemovalError=Suuren prioriteetin käynnistyspalvelua ei voitu poistaa. Asennuksen poistaminen peruttiin. Hyväksy järjestelmänvalvojan pyyntö ja yritä uudelleen.
french.AdditionalTasks=Tâches supplémentaires :
french.StartupTaskDescription=Démarrer WallpaperSwitch à l’ouverture de session Windows (priorité standard ; activez la priorité élevée dans l’application)
french.ServiceRemovalError=Impossible de supprimer le service de démarrage à priorité élevée. La désinstallation a été annulée. Autorisez la demande d’administration et réessayez.
german.AdditionalTasks=Zusätzliche Aufgaben:
german.StartupTaskDescription=WallpaperSwitch bei der Windows-Anmeldung starten (Standardpriorität; hohe Priorität in der App aktivieren)
german.ServiceRemovalError=Der Startdienst mit hoher Priorität konnte nicht entfernt werden. Die Deinstallation wurde abgebrochen. Bestätigen Sie die Administratorabfrage und versuchen Sie es erneut.
hebrew.AdditionalTasks=משימות נוספות:
hebrew.StartupTaskDescription=הפעלת WallpaperSwitch בעת הכניסה ל-Windows (עדיפות רגילה; הפעלת עדיפות גבוהה באפליקציה)
hebrew.ServiceRemovalError=לא ניתן להסיר את שירות ההפעלה בעדיפות גבוהה. הסרת ההתקנה בוטלה. אשרו את בקשת מנהל המערכת ונסו שוב.
hungarian.AdditionalTasks=További feladatok:
hungarian.StartupTaskDescription=WallpaperSwitch indítása a Windowsba való bejelentkezéskor (normál prioritás; a magas prioritást az alkalmazásban kapcsolhatja be)
hungarian.ServiceRemovalError=A magas prioritású indítási szolgáltatás eltávolítása nem sikerült. A program eltávolítása megszakadt. Hagyja jóvá a rendszergazdai kérést, majd próbálja újra.
italian.AdditionalTasks=Attività aggiuntive:
italian.StartupTaskDescription=Avvia WallpaperSwitch all’accesso a Windows (priorità standard; attiva la priorità alta nell’app)
italian.ServiceRemovalError=Impossibile rimuovere il servizio di avvio ad alta priorità. La disinstallazione è stata annullata. Approva la richiesta di amministratore e riprova.
japanese.AdditionalTasks=追加タスク:
japanese.StartupTaskDescription=Windows へのサインイン時に WallpaperSwitch を起動する（通常の優先度。高優先度はアプリで有効にできます）
japanese.ServiceRemovalError=高優先度のスタートアップサービスを削除できませんでした。アンインストールはキャンセルされました。管理者の許可を行ってから、もう一度お試しください。
korean.AdditionalTasks=추가 작업:
korean.StartupTaskDescription=Windows에 로그인할 때 WallpaperSwitch 시작(일반 우선순위, 높은 우선순위는 앱에서 켤 수 있음)
korean.ServiceRemovalError=높은 우선순위 시작 서비스를 제거하지 못했습니다. 제거 작업이 취소되었습니다. 관리자 권한 요청을 승인한 후 다시 시도하세요.
norwegian.AdditionalTasks=Flere oppgaver:
norwegian.StartupTaskDescription=Start WallpaperSwitch når du logger på Windows (standard prioritet; aktiver høy prioritet i appen)
norwegian.ServiceRemovalError=Oppstartstjenesten med høy prioritet kunne ikke fjernes. Avinstalleringen ble avbrutt. Godkjenn administratorforespørselen og prøv på nytt.
polish.AdditionalTasks=Dodatkowe zadania:
polish.StartupTaskDescription=Uruchamiaj WallpaperSwitch po zalogowaniu do systemu Windows (standardowy priorytet; wysoki priorytet włącz w aplikacji)
polish.ServiceRemovalError=Nie można usunąć usługi uruchamiania o wysokim priorytecie. Odinstalowywanie zostało anulowane. Zatwierdź żądanie administratora i spróbuj ponownie.
portuguese.AdditionalTasks=Tarefas adicionais:
portuguese.StartupTaskDescription=Iniciar o WallpaperSwitch ao iniciar sessão no Windows (prioridade normal; ative a prioridade alta na aplicação)
portuguese.ServiceRemovalError=Não foi possível remover o serviço de arranque de prioridade alta. A desinstalação foi cancelada. Aprove o pedido de administrador e tente novamente.
russian.AdditionalTasks=Дополнительные задачи:
russian.StartupTaskDescription=Запускать WallpaperSwitch при входе в Windows (обычный приоритет; высокий приоритет можно включить в приложении)
russian.ServiceRemovalError=Не удалось удалить службу запуска с высоким приоритетом. Удаление отменено. Подтвердите запрос администратора и повторите попытку.
slovak.AdditionalTasks=Ďalšie úlohy:
slovak.StartupTaskDescription=Spúšťať WallpaperSwitch pri prihlasovaní do Windows (štandardná priorita; vysokú prioritu zapnite v aplikácii)
slovak.ServiceRemovalError=Službu spúšťania s vysokou prioritou sa nepodarilo odstrániť. Odinštalovanie bolo zrušené. Potvrďte žiadosť správcu a skúste to znova.
slovenian.AdditionalTasks=Dodatna opravila:
slovenian.StartupTaskDescription=Zaženi WallpaperSwitch ob prijavi v Windows (običajna prednost; visoko prednost omogočite v aplikaciji)
slovenian.ServiceRemovalError=Storitve zagona z visoko prednostjo ni bilo mogoče odstraniti. Odstranitev je bila preklicana. Potrdite skrbniško zahtevo in poskusite znova.
spanish.AdditionalTasks=Tareas adicionales:
spanish.StartupTaskDescription=Iniciar WallpaperSwitch al iniciar sesión en Windows (prioridad estándar; activa la prioridad alta en la aplicación)
spanish.ServiceRemovalError=No se pudo quitar el servicio de inicio de prioridad alta. Se canceló la desinstalación. Aprueba la solicitud del administrador e inténtalo de nuevo.
swedish.AdditionalTasks=Ytterligare uppgifter:
swedish.StartupTaskDescription=Starta WallpaperSwitch när du loggar in i Windows (standardprioritet; aktivera hög prioritet i appen)
swedish.ServiceRemovalError=Det gick inte att ta bort starttjänsten med hög prioritet. Avinstallationen avbröts. Godkänn administratörsbegäran och försök igen.
tamil.AdditionalTasks=கூடுதல் பணிகள்:
tamil.StartupTaskDescription=Windows இல் உள்நுழையும்போது WallpaperSwitch ஐத் தொடங்கு (சாதாரண முன்னுரிமை; உயர் முன்னுரிமையைப் பயன்பாட்டில் இயக்கவும்)
tamil.ServiceRemovalError=உயர் முன்னுரிமை தொடக்கச் சேவையை அகற்ற முடியவில்லை. நிறுவல் நீக்கம் ரத்து செய்யப்பட்டது. நிர்வாகி அனுமதி கோரிக்கையை ஏற்று மீண்டும் முயற்சிக்கவும்.
thai.AdditionalTasks=งานเพิ่มเติม:
thai.StartupTaskDescription=เริ่ม WallpaperSwitch เมื่อลงชื่อเข้าใช้ Windows (ลำดับความสำคัญปกติ; เปิดลำดับความสำคัญสูงได้ในแอป)
thai.ServiceRemovalError=ไม่สามารถนำบริการเริ่มต้นระบบที่มีลำดับความสำคัญสูงออกได้ ยกเลิกการถอนการติดตั้งแล้ว โปรดยอมรับคำขอสิทธิ์ผู้ดูแลระบบแล้วลองอีกครั้ง
turkish.AdditionalTasks=Ek görevler:
turkish.StartupTaskDescription=Windows’ta oturum açtığınızda WallpaperSwitch’i başlat (standart öncelik; yüksek önceliği uygulamada etkinleştirin)
turkish.ServiceRemovalError=Yüksek öncelikli başlangıç hizmeti kaldırılamadı. Kaldırma işlemi iptal edildi. Yönetici iznini onaylayıp yeniden deneyin.
ukrainian.AdditionalTasks=Додаткові завдання:
ukrainian.StartupTaskDescription=Запускати WallpaperSwitch під час входу у Windows (звичайний пріоритет; високий пріоритет увімкніть у програмі)
ukrainian.ServiceRemovalError=Не вдалося видалити службу запуску з високим пріоритетом. Видалення скасовано. Підтвердьте запит адміністратора й повторіть спробу.
chinesesimplified.AdditionalTasks=附加任务：
chinesesimplified.StartupTaskDescription=登录 Windows 时启动 WallpaperSwitch（标准优先级；如需高优先级，请在应用中手动开启）
chinesesimplified.ServiceRemovalError=无法移除高优先级启动服务，已取消卸载。请完成管理员授权后重试。
chinesetraditional.AdditionalTasks=其他工作：
chinesetraditional.StartupTaskDescription=登入 Windows 時啟動 WallpaperSwitch（標準優先順序；如需高優先順序，請在應用程式中手動啟用）
chinesetraditional.ServiceRemovalError=無法移除高優先順序啟動服務，已取消解除安裝。請完成管理員授權後再試一次。

[Tasks]
Name: "startup"; Description: "{cm:StartupTaskDescription}"; GroupDescription: "{cm:AdditionalTasks}"; Flags: checkedonce; Check: AllowStandardStartup

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[InstallDelete]
; Version 1.2 shipped loose XBF files and resources.pri. MRT can load these
; before the current WallpaperSwitch.pri, breaking WinUI and translated strings.
; These exact obsolete files are absent from the current publish payload.
Type: files; Name: "{app}\resources.pri"
Type: files; Name: "{app}\App.xbf"
Type: files; Name: "{app}\MainWindow.xbf"

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{group}\{cm:UninstallProgram,WallpaperSwitch}"; Filename: "{uninstallexe}"

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "WallpaperSwitch"; ValueData: """{app}\{#MyAppExeName}"" --minimized"; Flags: uninsdeletevalue; Tasks: startup

[Run]
Filename: "{app}\StartupService\WallpaperSwitch.StartupHost.exe"; Parameters: "--refresh-user"; Flags: runhidden waituntilterminated; Check: HasHighPriorityStartup
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,WallpaperSwitch}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Code]
function HasHighPriorityStartup: Boolean;
var
  UserKeys: TArrayOfString;
  I: Integer;
  AppPath: String;
begin
  Result := False;
  if RegGetSubkeyNames(HKLM64, 'SOFTWARE\WallpaperSwitch\StartupUsers', UserKeys) then
    for I := 0 to GetArrayLength(UserKeys) - 1 do
      if RegQueryStringValue(HKLM64, 'SOFTWARE\WallpaperSwitch\StartupUsers\' + UserKeys[I], 'ApplicationPath', AppPath) then
        if CompareText(AppPath, ExpandConstant('{app}\{#MyAppExeName}')) = 0 then
          Result := True;
end;

function AllowStandardStartup: Boolean;
begin
  Result := not HasHighPriorityStartup;
end;

procedure InitializeWizard;
begin
  if RegKeyExists(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{8F3A2B91-6D4E-4C17-A9B8-E1F0C4D7A256}_is1') then
  begin
    if RegValueExists(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'WallpaperSwitch') and AllowStandardStartup then
      WizardSelectTasks('startup')
    else
      WizardSelectTasks('!startup');
  end;
end;

function InitializeUninstall: Boolean;
var
  ResultCode: Integer;
begin
  Result := True;
  if HasHighPriorityStartup then
  begin
    Result := Exec(ExpandConstant('{app}\StartupService\WallpaperSwitch.StartupHost.exe'),
      '--uninstall-user', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    if Result then Result := ResultCode = 0;
    if not Result then
      MsgBox(CustomMessage('ServiceRemovalError'), mbError, MB_OK);
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  SettingsDir, SettingsFile, StartupEnabled: String;
begin
  if CurStep = ssPostInstall then
  begin
    SettingsDir := ExpandConstant('{localappdata}\WallpaperSwitch');
    ForceDirectories(SettingsDir);
    SettingsFile := SettingsDir + '\settings.json';
    if WizardIsTaskSelected('startup') then StartupEnabled := 'true'
    else StartupEnabled := 'false';
    if not FileExists(SettingsFile) then
      SaveStringToFile(SettingsFile,
        '{"isEnabled":true,"switchAppTheme":true,"switchSystemTheme":true,"switchWallpaper":true,"syncLockScreen":true,"launchAtStartup":' + StartupEnabled + ',"highPriorityStartup":false,"minimizeToTray":true,"scheduleMode":"Custom","wallpaperFit":"Fill"}',
        False);
  end;
end;
