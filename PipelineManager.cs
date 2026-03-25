using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Vision
{
    /// <summary>
    /// 여러 PipelineConfig를 관리하고 단일 XML 파일로 저장/로드한다.
    ///
    /// 역할:
    ///   - PipelineConfig 컬렉션 관리 (추가/삭제/활성화)
    ///   - pipelines.xml 저장 및 로드 (IStepSerializable 위임)
    ///   - 활성 파이프라인 인덱스 추적
    ///
    /// 저장 경로: {생성자에 전달한 folder}/pipelines.xml
    /// </summary>
    public class PipelineManager
    {
        private readonly List<PipelineConfig> _configs = new List<PipelineConfig>();
        private int _activeIndex = 0;
        private readonly string _folder;

        private static readonly string FileName = "pipelines.xml";

        // 관리 중인 파이프라인 설정 목록 (읽기 전용)
        public IReadOnlyList<PipelineConfig> Configs => _configs;

        // 현재 활성 파이프라인 설정. 목록이 비어 있거나 인덱스 범위를 벗어나면 null 반환
        public PipelineConfig ActivePipeline =>
            (_activeIndex >= 0 && _activeIndex < _configs.Count)
                ? _configs[_activeIndex]
                : null;

        /// <summary>
        /// 활성 파이프라인의 인덱스. 0 이상, Configs.Count-1 이하로 자동 클램핑된다.
        /// </summary>
        public int ActiveIndex
        {
            get => _activeIndex;
            set => _activeIndex = Math.Max(0, Math.Min(value, Math.Max(0, _configs.Count - 1)));
        }


        /// <summary>파이프라인 XML 파일의 전체 경로.</summary>
        public string FilePath => Path.Combine(_folder, FileName);

        /// <summary>
        /// PipelineManager를 초기화한다.
        /// </summary>
        /// <param name="folder">pipelines.xml을 저장/로드할 디렉터리 경로</param>
        public PipelineManager(string folder)
        {
            _folder = folder;
        }

        // ── 추가 / 제거 ──────────────────────────────────────────────────

        /// <summary>
        /// 새 파이프라인 설정을 목록 끝에 추가하고 활성 인덱스를 해당 항목으로 이동한다.
        /// </summary>
        /// <param name="config">추가할 PipelineConfig</param>
        public void Add(PipelineConfig config)
        {
            config.SaveCallback = () => SaveAll();
            _configs.Add(config);
            _activeIndex = _configs.Count - 1;
        }

        /// <summary>
        /// 지정 인덱스의 파이프라인을 목록에서 제거한다.
        /// 활성 인덱스가 범위를 벗어나지 않도록 자동 조정한다.
        /// </summary>
        /// <param name="index">제거할 항목의 인덱스</param>
        public void RemoveAt(int index)
        {
            if (index < 0 || index >= _configs.Count) return;
            _configs.RemoveAt(index);
            _activeIndex = Math.Max(0, Math.Min(_activeIndex, _configs.Count - 1));
        }

        // ── 저장 ─────────────────────────────────────────────────────────

        // ── 저장 ─────────────────────────────────────────────────────────

        /// <summary>
        /// 매니페스트(pipelines.xml)와 모든 파이프라인 파일을 저장한다.
        /// 목록에 없는 고아 파이프라인 파일은 자동으로 삭제한다.
        /// </summary>
        public void SaveAll()
        {
            Directory.CreateDirectory(_folder);

            // 매니페스트 저장
            var root = new XElement("Pipelines", new XAttribute("active", _activeIndex));
            foreach (var cfg in _configs)
                root.Add(new XElement("Pipeline", new XAttribute("name", cfg.Name)));
            new XDocument(new XDeclaration("1.0", "utf-8", null), root).Save(FilePath);

            // 파이프라인별 파일 저장
            var activeFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var cfg in _configs)
            {
                SavePipelineFile(cfg);
                activeFiles.Add(Path.GetFileName(PipelineFilePath(cfg.Name)));
            }

            // 고아 파일 정리
            foreach (var file in Directory.GetFiles(_folder, "pipeline_*.xml"))
                if (!activeFiles.Contains(Path.GetFileName(file)))
                    try { File.Delete(file); } catch { }
        }

        /// <summary>
        /// 지정한 파이프라인 하나만 파일에 저장한다.
        /// Pipelines[index].Save() 내부에서 호출된다.
        /// </summary>
        public void SavePipeline(PipelineConfig config)
        {
            Directory.CreateDirectory(_folder);
            SavePipelineFile(config);
        }

        /// <summary>
        /// 파이프라인 이름을 변경하고 구 파일을 삭제한 뒤 새 파일로 저장한다.
        /// PipelineController.RenamePipeline()에서 호출된다.
        /// </summary>
        public void RenamePipeline(int index, string newName)
        {
            if (index < 0 || index >= _configs.Count) return;
            var cfg     = _configs[index];
            var oldPath = PipelineFilePath(cfg.Name);
            cfg.Name    = newName;
            try { if (File.Exists(oldPath)) File.Delete(oldPath); } catch { }
            Directory.CreateDirectory(_folder);
            SavePipelineFile(cfg);
        }

        private string PipelineFilePath(string name)
            => Path.Combine(_folder, "pipeline_" + SanitizeFileName(name) + ".xml");

        private static string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var sb = new System.Text.StringBuilder();
            foreach (char c in name)
                sb.Append(System.Array.IndexOf(invalid, c) >= 0 ? '_' : c);
            return sb.ToString();
        }

        private void SavePipelineFile(PipelineConfig config)
        {
            var cfgEl   = new XElement("Pipeline", new XAttribute("name", config.Name));
            var stepsEl = new XElement("Steps");

            foreach (var step in config.Steps)
            {
                var stepEl = new XElement("Step", new XAttribute("type", step.Name));
                if (step.DisplayName != step.Name)
                    stepEl.Add(new XAttribute("label", step.DisplayName));
                (step as IStepSerializable)?.SaveParams(stepEl);
                stepsEl.Add(stepEl);
            }

            cfgEl.Add(stepsEl);
            new XDocument(new XDeclaration("1.0", "utf-8", null), cfgEl)
                .Save(PipelineFilePath(config.Name));
        }

        // ── 로드 ─────────────────────────────────────────────────────────

        /// <summary>
        /// 매니페스트를 읽고 각 파이프라인 파일에서 스텝을 로드한다.
        /// 파일이 없으면 아무 작업도 하지 않는다.
        /// </summary>
        public void LoadAll(IReadOnlyDictionary<string, Func<IVisionStep>> stepFactories)
        {
            _configs.Clear();
            if (!File.Exists(FilePath)) return;

            XElement root;
            try   { root = XDocument.Load(FilePath).Root; }
            catch { return; }

            _activeIndex = (int?)root.Attribute("active") ?? 0;

            foreach (var el in root.Elements("Pipeline"))
            {
                var cfg = new PipelineConfig { Name = (string)el.Attribute("name") ?? "Pipeline" };

                var path = PipelineFilePath(cfg.Name);
                if (File.Exists(path))
                {
                    XElement pipelineEl;
                    try   { pipelineEl = XDocument.Load(path).Root; }
                    catch { pipelineEl = null; }
                    if (pipelineEl != null)
                        ParseSteps(pipelineEl, cfg, stepFactories);
                }

                AttachSaveCallback(cfg);
                _configs.Add(cfg);
            }

            _activeIndex = Math.Max(0, Math.Min(_activeIndex, _configs.Count - 1));
        }

        /// <summary>
        /// 인메모리 XElement(스냅샷)에서 파이프라인 설정을 복원한다.
        /// 스텝이 XElement 안에 inline으로 포함된 포맷을 사용한다.
        /// PipelineController가 Cancel 시 스냅샷 복원에 사용한다.
        /// </summary>
        public void LoadFromElement(XElement root,
            IReadOnlyDictionary<string, Func<IVisionStep>> stepFactories)
        {
            _configs.Clear();
            if (root == null) return;

            _activeIndex = (int?)root.Attribute("active") ?? 0;

            foreach (var el in root.Elements("Pipeline"))
            {
                var cfg = new PipelineConfig { Name = (string)el.Attribute("name") ?? "Pipeline" };
                ParseSteps(el, cfg, stepFactories);
                AttachSaveCallback(cfg);
                _configs.Add(cfg);
            }

            _activeIndex = Math.Max(0, Math.Min(_activeIndex, _configs.Count - 1));
        }

        private void ParseSteps(XElement pipelineEl, PipelineConfig cfg,
            IReadOnlyDictionary<string, Func<IVisionStep>> stepFactories)
        {
            foreach (var stepEl in pipelineEl.Element("Steps")?.Elements("Step")
                                   ?? Enumerable.Empty<XElement>())
            {
                string type = (string)stepEl.Attribute("type");
                Func<IVisionStep> factory;
                if (type == null || !stepFactories.TryGetValue(type, out factory)) continue;

                var step  = factory();
                string label = (string)stepEl.Attribute("label");
                if (!string.IsNullOrEmpty(label))
                    step.DisplayName = label;

                (step as IStepSerializable)?.LoadParams(stepEl);
                cfg.Steps.Add(step);
            }
        }

        private void AttachSaveCallback(PipelineConfig cfg)
            => cfg.SaveCallback = () => SavePipeline(cfg);
    }
}
