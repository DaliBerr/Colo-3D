using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kernel.GameState;
using Lonize.EventSystem;
using Lonize.Localization;
using Lonize.UI;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Kernel.UI
{
    [UIPrefab("Prefabs/UI/LoadMenuUI")]
    /// <summary>
    /// summary: 存档菜单（保存/读取共用）。负责扫描存档、生成条目、选中填充输入框，并触发 Save/Load/Delete 请求。
    /// </summary>
    public class LoadMenuUI : UIScreen
    {
        public override Status currentStatus { get; } = StatusList.InMenuStatus;

        [Header("Buttons")]
        public Button SaveBtn;
        public Button LoadBtn;
        public Button DeleteBtn;
        public Button BackBtn;
        public Button RefreshBtn;

        [Header("List")]
        public SaveItemEntry saveItemPrefab;
        public Transform saveListContent;
        public GameObject emptyHint;

        [Header("Input")]
        public TMP_InputField saveNameInputField;

        [Header("Save Scan")]
        [SerializeField] private string saveRootFolderName = "Saves";

        [Tooltip("用于扫描文件存档的扩展名（留空表示扫描全部文件，排除 .meta）。")]
        [SerializeField] private string[] scanExtensions = new[] { ".json", ".save", ".dat" };

        [Tooltip("是否启用输入框的名称过滤（开启后会根据输入内容隐藏不匹配的条目）。")]
        [SerializeField] private bool enableNameFilter = false;

        private string _currentFilterText = string.Empty;

        private readonly List<SaveItemEntry> _saveItemEntries = new List<SaveItemEntry>();
        private SaveItemEntry _selectedEntry;

        private bool _isRefreshing;

        private float _deleteConfirmDeadline = -1f;
        private string _deleteBtnOriginalLabel;

        public enum LoadMenuMode
        {
            Save,
            Load
        }

        public LoadMenuMode currentMode = LoadMenuMode.Load;

        /// <summary>
        /// summary: UIScreen 初始化入口：刷新模式相关的可见性/按钮状态。
        /// </summary>
        /// <returns>无</returns>
        protected override void OnInit()
        {
            RefreshModeVisuals();
        }

        /// <summary>
        /// summary: Unity OnEnable：绑定UI事件 + 刷新存档列表（打开时不自动填入输入框）。
        /// </summary>
        /// <returns>无</returns>
        private void OnEnable()
        {
            BindUiEvents();

            // 打开时不自动填入输入框，也不自动选中条目
            ResetInputAndSelectionOnOpen(true);

            StartCoroutine(CoRefreshSaveList());
        }

        /// <summary>
        /// summary: Unity OnDisable：解绑UI事件，避免重复注册。
        /// </summary>
        /// <returns>无</returns>
        private void OnDisable()
        {
            UnbindUiEvents();
            ResetDeleteConfirmationVisual();
        }

        /// <summary>
        /// summary: Unity Update：处理删除二次确认超时、回车快捷 Save/Load。
        /// </summary>
        /// <returns>无</returns>
        private void Update()
        {
            if (_deleteConfirmDeadline > 0f && Time.unscaledTime > _deleteConfirmDeadline)
                ResetDeleteConfirmationVisual();

            if (saveNameInputField == null)
                return;

            if (!saveNameInputField.isFocused)
                return;

            // if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            // {
            //     if (currentMode == LoadMenuMode.Load)
            //         TryLoad();
            //     else
            //         TrySave();
            // }
        }

        /// <summary>
        /// summary: 设置界面模式（保存/读取）。会同步刷新UI状态与存档列表。
        /// </summary>
        /// <param name="mode">目标模式</param>
        /// <returns>无</returns>
        public void SetMode(LoadMenuMode mode)
        {
            currentMode = mode;
            RefreshModeVisuals();

            // 切换模式时同样保持输入框为空，避免误操作
            ResetInputAndSelectionOnOpen(true);

            StartCoroutine(CoRefreshSaveList());
        }

        /// <summary>
        /// summary: 立刻刷新存档列表（外部可调用）。
        /// </summary>
        /// <returns>无</returns>
        public void RefreshNow()
        {
            StartCoroutine(CoRefreshSaveList());
        }

        /// <summary>
        /// summary: 打开界面或切换模式时重置输入框与选中项（避免初始自动填入、以及选中条目导致误过滤）。
        /// </summary>
        /// <param name="clearFilter">是否同时清空过滤文本</param>
        /// <returns>无</returns>
        private void ResetInputAndSelectionOnOpen(bool clearFilter)
        {
            SetSelectedEntry(null);

            if (saveNameInputField != null)
                saveNameInputField.SetTextWithoutNotify(string.Empty);

            if (clearFilter)
                _currentFilterText = string.Empty;

            if (enableNameFilter)
                ApplyFilter(_currentFilterText);

            RefreshActionButtons();
        }

        /// <summary>
        /// summary: 根据模式刷新按钮可见性/交互性。
        /// </summary>
        /// <returns>无</returns>
        private void RefreshModeVisuals()
        {
            if (SaveBtn != null) SaveBtn.gameObject.SetActive(currentMode == LoadMenuMode.Save);
            if (LoadBtn != null) LoadBtn.gameObject.SetActive(currentMode == LoadMenuMode.Load);

            // 删除与输入框：两种模式都允许（保存时可覆盖/删除旧档；读取时可手动输入名称）
            if (DeleteBtn != null) DeleteBtn.gameObject.SetActive(true);
            if (saveNameInputField != null) saveNameInputField.gameObject.SetActive(true);

            RefreshActionButtons();
        }

        /// <summary>
        /// summary: 绑定UI回调，避免在 OnEnable 多次叠加 listener。
        /// </summary>
        /// <returns>无</returns>
        private void BindUiEvents()
        {
            if (BackBtn != null)
            {
                BackBtn.onClick.RemoveListener(TryBack);
                BackBtn.onClick.AddListener(TryBack);
            }

            if (LoadBtn != null)
            {
                LoadBtn.onClick.RemoveListener(TryLoad);
                LoadBtn.onClick.AddListener(TryLoad);
            }

            if (SaveBtn != null)
            {
                SaveBtn.onClick.RemoveListener(TrySave);
                SaveBtn.onClick.AddListener(TrySave);
            }

            if (DeleteBtn != null)
            {
                CacheDeleteButtonLabelOnce();
                DeleteBtn.onClick.RemoveListener(TryDelete);
                DeleteBtn.onClick.AddListener(TryDelete);
            }

            if (RefreshBtn != null)
            {
                RefreshBtn.onClick.RemoveListener(RefreshNow);
                RefreshBtn.onClick.AddListener(RefreshNow);
            }

            if (saveNameInputField != null)
            {
                saveNameInputField.onValueChanged.RemoveListener(OnNameInputChanged);
                saveNameInputField.onValueChanged.AddListener(OnNameInputChanged);
            }
        }

        /// <summary>
        /// summary: 解绑UI回调。
        /// </summary>
        /// <returns>无</returns>
        private void UnbindUiEvents()
        {
            if (BackBtn != null) BackBtn.onClick.RemoveListener(TryBack);
            if (LoadBtn != null) LoadBtn.onClick.RemoveListener(TryLoad);
            if (SaveBtn != null) SaveBtn.onClick.RemoveListener(TrySave);
            if (DeleteBtn != null) DeleteBtn.onClick.RemoveListener(TryDelete);
            if (RefreshBtn != null) RefreshBtn.onClick.RemoveListener(RefreshNow);

            if (saveNameInputField != null)
                saveNameInputField.onValueChanged.RemoveListener(OnNameInputChanged);
        }

        /// <summary>
        /// summary: 输入框变更：用于过滤列表并刷新按钮状态（过滤只跟用户真实输入绑定）。
        /// </summary>
        /// <param name="text">输入文本</param>
        /// <returns>无</returns>
        private void OnNameInputChanged(string text)
        {
            // 过滤只跟“用户真实输入”绑定；选中条目写入输入框使用 SetTextWithoutNotify 不会触发这里
            _currentFilterText = text ?? string.Empty;

            if (enableNameFilter)
                ApplyFilter(_currentFilterText);

            RefreshActionButtons();
        }

        /// <summary>
        /// summary: 返回上一层UI。
        /// </summary>
        /// <returns>无</returns>
        private void TryBack()
        {
            UIManager.Instance.PopScreen();
        }

        /// <summary>
        /// summary: 触发读取存档请求（EventBus）。
        /// </summary>
        /// <returns>无</returns>
        private async void TryLoad()
        {
            string name = GetDesiredSaveName();
            if (string.IsNullOrWhiteSpace(name))
                return;
            if(!Path.HasExtension(name) && scanExtensions!=null && scanExtensions.Length>0){
                name += scanExtensions[0];
            }
            if(SceneManager.GetActiveScene().name!="Main"){

                await UIManager.Instance.RequestLoadGame(name);
                return;
            }
            else
            {
                Lonize.EventSystem.EventManager.eventBus.Publish(new EventList.LoadGameRequest(name));
            }
            
        }

        /// <summary>
        /// summary: 触发保存存档请求（EventBus）。
        /// </summary>
        /// <returns>无</returns>
        private void TrySave()
        {
            string name = GetDesiredSaveName();
            if (string.IsNullOrWhiteSpace(name))
                return;
            if(!Path.HasExtension(name) && scanExtensions!=null && scanExtensions.Length>0){
                name += scanExtensions[0];
            }
            Lonize.EventSystem.EventManager.eventBus.Publish(new EventList.SaveGameRequest(name));

            StartCoroutine(CoDelayedRefresh(0.15f));
        }

        /// <summary>
        /// summary: 删除存档（两次点击确认）。
        /// </summary>
        /// <returns>无</returns>
        private void TryDelete()
        {
            string name = GetDesiredSaveName();
            if (string.IsNullOrWhiteSpace(name))
                return;

            if (_deleteConfirmDeadline < 0f || Time.unscaledTime > _deleteConfirmDeadline)
            {
                ArmDeleteConfirmationVisual();
                return;
            }

            if (!TryResolveSavePath(name, out string fullPath, out bool isDirectory))
            {
                ResetDeleteConfirmationVisual();
                return;
            }

            try
            {
                // 当前实现“排除文件夹”，理论上只会删文件；保留 isDirectory 只是为了兼容未来扩展
                if (isDirectory)
                    Directory.Delete(fullPath, true);
                else
                    File.Delete(fullPath);
            }
            catch (Exception)
            {
                // 删除失败保持静默；需要日志可接入 Lonize.Logging.Log
            }

            ResetDeleteConfirmationVisual();

            SetSelectedEntry(null);

            if (saveNameInputField != null)
                saveNameInputField.SetTextWithoutNotify(string.Empty);

            _currentFilterText = string.Empty;
            if (enableNameFilter)
                ApplyFilter(_currentFilterText);

            StartCoroutine(CoRefreshSaveList());
        }

        /// <summary>
        /// summary: 延迟刷新列表（给存档系统一点落盘时间）。
        /// </summary>
        /// <param name="delaySeconds">延迟秒数（不受TimeScale影响）</param>
        /// <returns>协程</returns>
        private IEnumerator CoDelayedRefresh(float delaySeconds)
        {
            if (delaySeconds > 0f)
                yield return new WaitForSecondsRealtime(delaySeconds);

            yield return CoRefreshSaveList();
        }

        /// <summary>
        /// summary: 刷新存档列表（扫描目录、生成条目）。
        /// </summary>
        /// <returns>协程</returns>
        private IEnumerator CoRefreshSaveList()
        {
            if (_isRefreshing)
                yield break;

            _isRefreshing = true;

            yield return null;

            ClearSaveEntries();

            if (saveItemPrefab == null || saveListContent == null)
            {
                UpdateEmptyHint(true);
                _isRefreshing = false;
                yield break;
            }

            List<SaveMeta> saves = CollectSaveMetas();
            saves = saves.OrderByDescending(s => s.LastWriteTimeUtc).ToList();

            foreach (SaveMeta meta in saves)
            {
                SaveItemEntry entry = Instantiate(saveItemPrefab, saveListContent);
                entry.Initialize(meta.Name, meta.LocalTimeString);
                entry.Bind(OnSaveEntryClicked);
                entry.SetSelected(false);
                _saveItemEntries.Add(entry);
            }

            UpdateEmptyHint(_saveItemEntries.Count == 0);

            // 不自动选中任何条目，也不把名称写入输入框（避免打开时输入框被填入/误过滤）。
            RefreshActionButtons();

            // 过滤只跟“用户真实输入的过滤文本”走，不使用输入框当前内容（因为选中条目也会写入输入框）。
            if (enableNameFilter)
                ApplyFilter(_currentFilterText);

            _isRefreshing = false;
        }

        /// <summary>
        /// summary: 清空当前生成的存档条目。
        /// </summary>
        /// <returns>无</returns>
        private void ClearSaveEntries()
        {
            foreach (SaveItemEntry entry in _saveItemEntries)
            {
                if (entry != null)
                    Destroy(entry.gameObject);
            }

            _saveItemEntries.Clear();
            SetSelectedEntry(null);
        }

        /// <summary>
        /// summary: 点击存档条目：设置选中、高亮，并把名字写入输入框（不触发过滤）。
        /// </summary>
        /// <param name="entry">被点击的条目</param>
        /// <returns>无</returns>
        private void OnSaveEntryClicked(SaveItemEntry entry)
        {
            if (entry == null)
                return;

            SetSelectedEntry(entry);

            // 注意：这里用 SetTextWithoutNotify，避免触发 onValueChanged -> ApplyFilter
            if (saveNameInputField != null)
                saveNameInputField.SetTextWithoutNotify(entry.SaveName ?? string.Empty);

            RefreshActionButtons();
        }

        /// <summary>
        /// summary: 设置当前选中条目并刷新显示。
        /// </summary>
        /// <param name="entry">新的选中条目（可为 null）</param>
        /// <returns>无</returns>
        private void SetSelectedEntry(SaveItemEntry entry)
        {
            if (_selectedEntry == entry)
                return;

            if (_selectedEntry != null)
                _selectedEntry.SetSelected(false);

            _selectedEntry = entry;

            if (_selectedEntry != null)
                _selectedEntry.SetSelected(true);
        }

        /// <summary>
        /// summary: 根据输入文本对列表进行简单过滤（包含匹配）。
        /// </summary>
        /// <param name="filter">过滤关键字</param>
        /// <returns>无</returns>
        private void ApplyFilter(string filter)
        {
            if (_saveItemEntries.Count == 0)
                return;

            string f = (filter ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(f))
            {
                foreach (SaveItemEntry e in _saveItemEntries)
                {
                    if (e != null)
                        e.gameObject.SetActive(true);
                }

                return;
            }

            foreach (SaveItemEntry e in _saveItemEntries)
            {
                if (e == null)
                    continue;

                bool active = !string.IsNullOrEmpty(e.SaveName) &&
                              e.SaveName.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0;

                e.gameObject.SetActive(active);
            }

            if (_selectedEntry != null && !_selectedEntry.gameObject.activeSelf)
                SetSelectedEntry(null);
        }

        /// <summary>
        /// summary: 刷新 Save/Load/Delete 按钮交互性（避免空名称误触）。
        /// </summary>
        /// <returns>无</returns>
        private void RefreshActionButtons()
        {
            string name = GetDesiredSaveName();
            bool hasName = !string.IsNullOrWhiteSpace(name);

            if (LoadBtn != null)
                LoadBtn.interactable = hasName;

            if (SaveBtn != null)
                SaveBtn.interactable = hasName;

            if (DeleteBtn != null)
                DeleteBtn.interactable = hasName;
        }

        /// <summary>
        /// summary: 从输入框/选中项获取当前操作目标的存档名。
        /// </summary>
        /// <returns>存档名（可能为空）</returns>
        private string GetDesiredSaveName()
        {
            if (saveNameInputField == null)
                return _selectedEntry != null ? _selectedEntry.SaveName : string.Empty;

            string text = saveNameInputField.text ?? string.Empty;
            text = text.Trim();

            if (!string.IsNullOrEmpty(text))
                return text;

            return _selectedEntry != null ? (_selectedEntry.SaveName ?? string.Empty) : string.Empty;
        }

        /// <summary>
        /// summary: 收集存档元数据（仅文件式存档，排除文件夹）。
        /// </summary>
        /// <returns>存档列表</returns>
        private List<SaveMeta> CollectSaveMetas()
        {
            string root = GetSaveRootPath();
            EnsureDirectory(root);

            var dict = new Dictionary<string, SaveMeta>(StringComparer.OrdinalIgnoreCase);

            try
            {
                IEnumerable<string> files = Directory.EnumerateFiles(root);
                bool useExtFilter = scanExtensions != null && scanExtensions.Length > 0;

                foreach (string file in files)
                {
                    if (file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string ext = Path.GetExtension(file);
                    if (useExtFilter)
                    {
                        bool matched = false;
                        foreach (string allowed in scanExtensions)
                        {
                            if (string.IsNullOrEmpty(allowed))
                                continue;

                            if (string.Equals(ext, allowed, StringComparison.OrdinalIgnoreCase))
                            {
                                matched = true;
                                break;
                            }
                        }

                        if (!matched)
                            continue;
                    }

                    string name = Path.GetFileNameWithoutExtension(file);
                    if (string.IsNullOrEmpty(name))
                        continue;

                    DateTime utc = File.GetLastWriteTimeUtc(file);
                    var meta = SaveMeta.Create(name, file, utc, false);
                    UpsertMeta(dict, meta);
                }
            }
            catch (Exception) { }

            return dict.Values.ToList();
        }

        /// <summary>
        /// summary: 向字典写入/更新元数据（同名时保留更新时间最新的）。
        /// </summary>
        /// <param name="dict">目标字典</param>
        /// <param name="meta">待写入的 meta</param>
        /// <returns>无</returns>
        private void UpsertMeta(Dictionary<string, SaveMeta> dict, SaveMeta meta)
        {
            if (dict == null || string.IsNullOrEmpty(meta.Name))
                return;

            if (dict.TryGetValue(meta.Name, out SaveMeta existing))
            {
                if (meta.LastWriteTimeUtc > existing.LastWriteTimeUtc)
                    dict[meta.Name] = meta;
            }
            else
            {
                dict[meta.Name] = meta;
            }
        }

        /// <summary>
        /// summary: 获取存档根目录（默认 persistentDataPath/Saves）。
        /// </summary>
        /// <returns>绝对路径</returns>
        private string GetSaveRootPath()
        {
            return Path.Combine(Application.persistentDataPath, saveRootFolderName);
        }

        /// <summary>
        /// summary: 确保目录存在。
        /// </summary>
        /// <param name="dir">目录路径</param>
        /// <returns>是否确保成功</returns>
        private bool EnsureDirectory(string dir)
        {
            try
            {
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// summary: 尝试根据存档名解析到具体文件路径（排除目录式存档）。
        /// </summary>
        /// <param name="saveName">存档名</param>
        /// <param name="fullPath">解析出的完整路径</param>
        /// <param name="isDirectory">是否为目录存档（当前实现应始终为 false）</param>
        /// <returns>是否解析成功</returns>
        private bool TryResolveSavePath(string saveName, out string fullPath, out bool isDirectory)
        {
            fullPath = null;
            isDirectory = false;

            if (string.IsNullOrWhiteSpace(saveName))
                return false;

            string root = GetSaveRootPath();
            if (!Directory.Exists(root))
                return false;

            // 1) 文件式：先按扩展名试
            if (scanExtensions != null && scanExtensions.Length > 0)
            {
                foreach (string ext in scanExtensions)
                {
                    if (string.IsNullOrEmpty(ext))
                        continue;

                    string path = Path.Combine(root, saveName + ext);
                    if (File.Exists(path))
                    {
                        fullPath = path;
                        isDirectory = false;
                        return true;
                    }
                }
            }

            // 2) 文件式兜底：扫描同名（不管扩展名）
            try
            {
                string file = Directory.EnumerateFiles(root)
                    .FirstOrDefault(f =>
                    {
                        if (f.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                            return false;

                        return string.Equals(Path.GetFileNameWithoutExtension(f), saveName,
                            StringComparison.OrdinalIgnoreCase);
                    });

                if (!string.IsNullOrEmpty(file) && File.Exists(file))
                {
                    fullPath = file;
                    isDirectory = false;
                    return true;
                }
            }
            catch (Exception) { }

            return false;
        }

        /// <summary>
        /// summary: 更新空列表提示显隐。
        /// </summary>
        /// <param name="isEmpty">是否为空</param>
        /// <returns>无</returns>
        private void UpdateEmptyHint(bool isEmpty)
        {
            if (emptyHint != null)
                emptyHint.SetActive(isEmpty);
        }

        /// <summary>
        /// summary: 记录 Delete 按钮原始文案（用于二次确认恢复）。
        /// </summary>
        /// <returns>无</returns>
        private void CacheDeleteButtonLabelOnce()
        {
            if (!string.IsNullOrEmpty(_deleteBtnOriginalLabel))
                return;

            _deleteBtnOriginalLabel = GetDeleteButtonLabel();
        }

        /// <summary>
        /// summary: 进入删除二次确认状态（2 秒内再次点击才删除）。
        /// </summary>
        /// <returns>无</returns>
        private void ArmDeleteConfirmationVisual()
        {
            _deleteConfirmDeadline = Time.unscaledTime + 2f;
            SetDeleteButtonLabel("Confirm Delete".Translate());
        }

        /// <summary>
        /// summary: 退出删除二次确认状态并恢复按钮文案。
        /// </summary>
        /// <returns>无</returns>
        private void ResetDeleteConfirmationVisual()
        {
            _deleteConfirmDeadline = -1f;

            if (!string.IsNullOrEmpty(_deleteBtnOriginalLabel))
                SetDeleteButtonLabel(_deleteBtnOriginalLabel);
        }

        /// <summary>
        /// summary: 获取 Delete 按钮当前文案（兼容 TMP/Text）。
        /// </summary>
        /// <returns>文案</returns>
        private string GetDeleteButtonLabel()
        {
            if (DeleteBtn == null)
                return string.Empty;

            TextMeshProUGUI tmp = DeleteBtn.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null)
                return tmp.text;

            Text legacy = DeleteBtn.GetComponentInChildren<Text>();
            if (legacy != null)
                return legacy.text;

            return string.Empty;
        }

        /// <summary>
        /// summary: 设置 Delete 按钮文案（兼容 TMP/Text）。
        /// </summary>
        /// <param name="label">新文案</param>
        /// <returns>无</returns>
        private void SetDeleteButtonLabel(string label)
        {
            if (DeleteBtn == null)
                return;

            TextMeshProUGUI tmp = DeleteBtn.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.text = label;
                return;
            }

            Text legacy = DeleteBtn.GetComponentInChildren<Text>();
            if (legacy != null)
                legacy.text = label;
        }

        /// <summary>
        /// summary: 存档元数据（文件/目录）。
        /// </summary>
        private readonly struct SaveMeta
        {
            public readonly string Name;
            public readonly string FullPath;
            public readonly DateTime LastWriteTimeUtc;
            public readonly bool IsDirectory;

            public string LocalTimeString
            {
                get
                {
                    DateTime local = LastWriteTimeUtc.ToLocalTime();
                    return local.ToString("yyyy-MM-dd HH:mm");
                }
            }

            /// <summary>
            /// summary: 创建 meta。
            /// </summary>
            /// <param name="name">存档名</param>
            /// <param name="fullPath">完整路径</param>
            /// <param name="lastWriteUtc">最后写入时间（UTC）</param>
            /// <param name="isDirectory">是否目录式</param>
            /// <returns>meta</returns>
            public static SaveMeta Create(string name, string fullPath, DateTime lastWriteUtc, bool isDirectory)
            {
                return new SaveMeta(name, fullPath, lastWriteUtc, isDirectory);
            }

            private SaveMeta(string name, string fullPath, DateTime lastWriteUtc, bool isDirectory)
            {
                Name = name;
                FullPath = fullPath;
                LastWriteTimeUtc = lastWriteUtc;
                IsDirectory = isDirectory;
            }
        }
    }
}
