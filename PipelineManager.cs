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

        /// <summary>
        /// 현재 관리 중인 모든 파이프라인을 XML 파일로 저장한다.
        ///
        /// 각 스텝이 IStepSerializable을 구현하면 SaveParams()를 호출하여
        /// 스텝별 파라미터(Region, RunParams 등)를 Step 요소에 기록한다.
        /// 대상 디렉터리가 없으면 자동으로 생성한다.
        /// </summary>
        public void SaveAll()
        {
            Directory.CreateDirectory(_folder);

            // 루트 요소에 현재 활성 인덱스를 속성으로 기록
            var root = new XElement("Pipelines", new XAttribute("active", _activeIndex));

            foreach (var cfg in _configs)
            {
                var cfgEl   = new XElement("Pipeline", new XAttribute("name", cfg.Name));
                var stepsEl = new XElement("Steps");

                foreach (var step in cfg.Steps)
                {
                    // type 속성으로 스텝 종류를 식별 (LoadAll에서 팩토리 조회 시 사용)
                    var stepEl = new XElement("Step", new XAttribute("type", step.Name));

                    // 사용자 정의 표시 이름이 있을 때만 label 속성으로 저장
                    if (step.DisplayName != step.Name)
                        stepEl.Add(new XAttribute("label", step.DisplayName));

                    // IStepSerializable을 구현한 스텝만 파라미터를 저장
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
        /// XML 파일에서 파이프라인 설정을 로드한다.
        ///
        /// 스텝 생성은 외부에서 제공한 stepFactories 딕셔너리를 통해 수행한다.
        /// 이를 통해 Vision.dll은 GUI 어셈블리(StepDescriptor 등)에 의존하지 않는다.
        ///
        /// XML에 기록된 type 속성으로 팩토리를 조회하고, 팩토리가 없는 스텝은 건너뛴다.
        /// 각 스텝이 IStepSerializable을 구현하면 LoadParams()를 호출하여 파라미터를 복원한다.
        /// XML 파일이 없으면 아무 작업도 수행하지 않는다.
        /// </summary>
        /// <param name="stepFactories">
        /// TypeName(스텝 Name) → 스텝 인스턴스 생성 팩토리 딕셔너리.
        /// 예: { "VisionPro.Caliper" => () => new CogCaliperStep() }
        /// </param>
        public void LoadAll(IReadOnlyDictionary<string, Func<IVisionStep>> stepFactories)
        {
            _configs.Clear();
            if (!File.Exists(FilePath)) return;

            XElement root;
            try   { root = XDocument.Load(FilePath).Root; }
            catch { return; }  // 파일 파싱 실패 시 조용히 무시

            // active 속성에서 마지막으로 선택된 파이프라인 인덱스 복원
            _activeIndex = (int?)root.Attribute("active") ?? 0;

            foreach (var cfgEl in root.Elements("Pipeline"))
            {
                var cfg  = new PipelineConfig();
                cfg.Name = (string)cfgEl.Attribute("name") ?? "Pipeline";

                foreach (var stepEl in cfgEl.Element("Steps")?.Elements("Step")
                                       ?? Enumerable.Empty<XElement>())
                {
                    string type = (string)stepEl.Attribute("type");

                    // 알 수 없는 type은 건너뜀 (미등록 스텝, 플러그인 제거 등 대비)
                    Func<IVisionStep> factory;
                    if (type == null || !stepFactories.TryGetValue(type, out factory)) continue;

                    var step = factory();

                    // label 속성이 있으면 표시 이름 복원
                    string label = (string)stepEl.Attribute("label");
                    if (!string.IsNullOrEmpty(label))
                        step.DisplayName = label;

                    // IStepSerializable 구현 스텝은 저장된 파라미터를 복원
                    (step as IStepSerializable)?.LoadParams(stepEl);
                    cfg.Steps.Add(step);
                }

                _configs.Add(cfg);
            }

            // 저장된 인덱스가 로드된 목록 범위를 벗어나지 않도록 클램핑
            _activeIndex = Math.Max(0, Math.Min(_activeIndex, _configs.Count - 1));
        }
    }
}
