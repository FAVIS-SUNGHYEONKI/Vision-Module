using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Vision
{
    /// <summary>
    /// 여러 PipelineConfig를 관리하고 단일 XML 파일로 저장/로드합니다.
    /// 저장 경로: {folder}/pipelines.xml
    /// </summary>
    public class PipelineManager
    {
        private readonly List<PipelineConfig> _configs = new List<PipelineConfig>();
        private int    _activeIndex = 0;
        private readonly string _folder;

        private static readonly string FileName = "pipelines.xml";

        public IReadOnlyList<PipelineConfig> Configs => _configs;

        public PipelineConfig ActivePipeline =>
            _configs.Count > 0 && _activeIndex < _configs.Count
                ? _configs[_activeIndex] : null;

        public int ActiveIndex
        {
            get => _activeIndex;
            set => _activeIndex = Math.Max(0, Math.Min(value, Math.Max(0, _configs.Count - 1)));
        }

        public string FilePath => Path.Combine(_folder, FileName);

        public PipelineManager(string folder)
        {
            _folder = folder;
        }

        // ── 추가 / 제거 ──────────────────────────────────────────────────

        public void Add(PipelineConfig config)
        {
            _configs.Add(config);
            _activeIndex = _configs.Count - 1;
        }

        public void RemoveAt(int index)
        {
            if (index < 0 || index >= _configs.Count) return;
            _configs.RemoveAt(index);
            _activeIndex = Math.Max(0, Math.Min(_activeIndex, _configs.Count - 1));
        }

        // ── 저장 ─────────────────────────────────────────────────────────

        public void SaveAll()
        {
            Directory.CreateDirectory(_folder);

            var root = new XElement("Pipelines", new XAttribute("active", _activeIndex));

            foreach (var cfg in _configs)
            {
                var cfgEl   = new XElement("Pipeline", new XAttribute("name", cfg.Name));
                var stepsEl = new XElement("Steps");

                foreach (var step in cfg.Steps)
                {
                    var stepEl = new XElement("Step", new XAttribute("type", step.Name));
                    (step as IStepSerializable)?.SaveParams(stepEl);
                    stepsEl.Add(stepEl);
                }

                cfgEl.Add(stepsEl);
                root.Add(cfgEl);
            }

            new XDocument(new XDeclaration("1.0", "utf-8", null), root).Save(FilePath);
        }

        // ── 로드 ─────────────────────────────────────────────────────────

        /// <summary>
        /// XML에서 파이프라인을 로드합니다.
        /// <paramref name="stepFactories"/>: TypeName → 스텝 인스턴스 생성 팩토리
        /// </summary>
        public void LoadAll(IReadOnlyDictionary<string, Func<IVisionStep>> stepFactories)
        {
            _configs.Clear();
            if (!File.Exists(FilePath)) return;

            XElement root;
            try   { root = XDocument.Load(FilePath).Root; }
            catch { return; }

            _activeIndex = (int?)root.Attribute("active") ?? 0;

            foreach (var cfgEl in root.Elements("Pipeline"))
            {
                var cfg  = new PipelineConfig();
                cfg.Name = (string)cfgEl.Attribute("name") ?? "Pipeline";

                foreach (var stepEl in cfgEl.Element("Steps")?.Elements("Step")
                                       ?? Enumerable.Empty<XElement>())
                {
                    string type = (string)stepEl.Attribute("type");
                    Func<IVisionStep> factory;
                    if (type == null || !stepFactories.TryGetValue(type, out factory)) continue;

                    var step = factory();
                    (step as IStepSerializable)?.LoadParams(stepEl);
                    cfg.Steps.Add(step);
                }

                _configs.Add(cfg);
            }

            _activeIndex = Math.Max(0, Math.Min(_activeIndex, _configs.Count - 1));
        }
    }
}
