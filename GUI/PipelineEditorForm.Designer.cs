namespace Vision.UI
{
    partial class PipelineEditorForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form 디자이너에서 생성한 코드

        private void InitializeComponent()
        {
            this.lblCognex = new System.Windows.Forms.Label();
            this.lstCognex = new System.Windows.Forms.ListBox();
            this.lblOpenCV = new System.Windows.Forms.Label();
            this.lstOpenCV = new System.Windows.Forms.ListBox();
            this.btnAdd = new System.Windows.Forms.Button();
            this.btnRemove = new System.Windows.Forms.Button();
            this.lblPipelineName = new System.Windows.Forms.Label();
            this.txtPipelineName = new System.Windows.Forms.TextBox();
            this.lblPipeline = new System.Windows.Forms.Label();
            this.lstPipeline = new System.Windows.Forms.ListBox();
            this.btnMoveUp = new System.Windows.Forms.Button();
            this.btnMoveDown = new System.Windows.Forms.Button();
            this.grpStepParams = new System.Windows.Forms.GroupBox();
            this.lblTestSection = new System.Windows.Forms.Label();
            this.btnSavePipeline = new System.Windows.Forms.Button();
            this.btnSaveStepParams = new System.Windows.Forms.Button();
            this.btnRunAll = new System.Windows.Forms.Button();
            this.btnSetTestRegion = new System.Windows.Forms.Button();
            this.btnTestRun = new System.Windows.Forms.Button();
            this.txtTestResult = new System.Windows.Forms.TextBox();
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // lblCognex
            this.lblCognex.AutoSize = true;
            this.lblCognex.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Bold);
            this.lblCognex.Location = new System.Drawing.Point(12, 40);
            this.lblCognex.Name = "lblCognex";
            this.lblCognex.Size = new System.Drawing.Size(78, 15);
            this.lblCognex.TabIndex = 20;
            this.lblCognex.Text = "Cognex 스텝";
            // lstCognex
            this.lstCognex.Font = new System.Drawing.Font("Consolas", 9.5F);
            this.lstCognex.FormattingEnabled = true;
            this.lstCognex.HorizontalScrollbar = true;
            this.lstCognex.ItemHeight = 15;
            this.lstCognex.Location = new System.Drawing.Point(12, 60);
            this.lstCognex.Name = "lstCognex";
            this.lstCognex.Size = new System.Drawing.Size(300, 184);
            this.lstCognex.TabIndex = 0;
            this.lstCognex.SelectedIndexChanged += new System.EventHandler(this.lstCognex_SelectedIndexChanged);
            this.lstCognex.DoubleClick += new System.EventHandler(this.lstCognex_DoubleClick);
            // lblOpenCV
            this.lblOpenCV.AutoSize = true;
            this.lblOpenCV.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Bold);
            this.lblOpenCV.Location = new System.Drawing.Point(12, 272);
            this.lblOpenCV.Name = "lblOpenCV";
            this.lblOpenCV.Size = new System.Drawing.Size(82, 15);
            this.lblOpenCV.TabIndex = 19;
            this.lblOpenCV.Text = "OpenCV 스텝";
            // lstOpenCV
            this.lstOpenCV.Font = new System.Drawing.Font("Consolas", 9.5F);
            this.lstOpenCV.FormattingEnabled = true;
            this.lstOpenCV.HorizontalScrollbar = true;
            this.lstOpenCV.ItemHeight = 15;
            this.lstOpenCV.Location = new System.Drawing.Point(12, 292);
            this.lstOpenCV.Name = "lstOpenCV";
            this.lstOpenCV.Size = new System.Drawing.Size(300, 154);
            this.lstOpenCV.TabIndex = 1;
            this.lstOpenCV.SelectedIndexChanged += new System.EventHandler(this.lstOpenCV_SelectedIndexChanged);
            this.lstOpenCV.DoubleClick += new System.EventHandler(this.lstOpenCV_DoubleClick);
            // btnAdd
            this.btnAdd.Enabled = false;
            this.btnAdd.Location = new System.Drawing.Point(327, 218);
            this.btnAdd.Name = "btnAdd";
            this.btnAdd.Size = new System.Drawing.Size(90, 32);
            this.btnAdd.TabIndex = 2;
            this.btnAdd.Text = "추가 →";
            this.btnAdd.UseVisualStyleBackColor = true;
            this.btnAdd.Click += new System.EventHandler(this.btnAdd_Click);
            // btnRemove
            this.btnRemove.Enabled = false;
            this.btnRemove.Location = new System.Drawing.Point(327, 265);
            this.btnRemove.Name = "btnRemove";
            this.btnRemove.Size = new System.Drawing.Size(90, 32);
            this.btnRemove.TabIndex = 3;
            this.btnRemove.Text = "← 제거";
            this.btnRemove.UseVisualStyleBackColor = true;
            this.btnRemove.Click += new System.EventHandler(this.btnRemove_Click);
            // lblPipelineName
            this.lblPipelineName.AutoSize = true;
            this.lblPipelineName.Font = new System.Drawing.Font("맑은 고딕", 9F);
            this.lblPipelineName.Location = new System.Drawing.Point(432, 14);
            this.lblPipelineName.Name = "lblPipelineName";
            this.lblPipelineName.Size = new System.Drawing.Size(34, 15);
            this.lblPipelineName.TabIndex = 18;
            this.lblPipelineName.Text = "이름:";
            // txtPipelineName
            this.txtPipelineName.Location = new System.Drawing.Point(472, 11);
            this.txtPipelineName.Name = "txtPipelineName";
            this.txtPipelineName.Size = new System.Drawing.Size(280, 21);
            this.txtPipelineName.TabIndex = 4;
            // lblPipeline
            this.lblPipeline.AutoSize = true;
            this.lblPipeline.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Bold);
            this.lblPipeline.Location = new System.Drawing.Point(432, 42);
            this.lblPipeline.Name = "lblPipeline";
            this.lblPipeline.Size = new System.Drawing.Size(95, 15);
            this.lblPipeline.TabIndex = 17;
            this.lblPipeline.Text = "파이프라인 순서";
            // lstPipeline
            this.lstPipeline.Font = new System.Drawing.Font("Consolas", 9.5F);
            this.lstPipeline.FormattingEnabled = true;
            this.lstPipeline.HorizontalScrollbar = true;
            this.lstPipeline.ItemHeight = 15;
            this.lstPipeline.Location = new System.Drawing.Point(432, 62);
            this.lstPipeline.Name = "lstPipeline";
            this.lstPipeline.Size = new System.Drawing.Size(400, 394);
            this.lstPipeline.TabIndex = 5;
            this.lstPipeline.SelectedIndexChanged += new System.EventHandler(this.lstPipeline_SelectedIndexChanged);
            // btnMoveUp
            this.btnMoveUp.Enabled = false;
            this.btnMoveUp.Location = new System.Drawing.Point(845, 62);
            this.btnMoveUp.Name = "btnMoveUp";
            this.btnMoveUp.Size = new System.Drawing.Size(65, 45);
            this.btnMoveUp.TabIndex = 6;
            this.btnMoveUp.Text = "▲ 위";
            this.btnMoveUp.UseVisualStyleBackColor = true;
            this.btnMoveUp.Click += new System.EventHandler(this.btnMoveUp_Click);
            // btnMoveDown
            this.btnMoveDown.Enabled = false;
            this.btnMoveDown.Location = new System.Drawing.Point(845, 117);
            this.btnMoveDown.Name = "btnMoveDown";
            this.btnMoveDown.Size = new System.Drawing.Size(65, 45);
            this.btnMoveDown.TabIndex = 7;
            this.btnMoveDown.Text = "▼ 아래";
            this.btnMoveDown.UseVisualStyleBackColor = true;
            this.btnMoveDown.Click += new System.EventHandler(this.btnMoveDown_Click);
            // grpStepParams
            this.grpStepParams.Font = new System.Drawing.Font("맑은 고딕", 9F);
            this.grpStepParams.Location = new System.Drawing.Point(922, 12);
            this.grpStepParams.Name = "grpStepParams";
            this.grpStepParams.Size = new System.Drawing.Size(530, 446);
            this.grpStepParams.TabIndex = 8;
            this.grpStepParams.TabStop = false;
            this.grpStepParams.Text = "스텝 파라미터";
            // btnSavePipeline
            this.btnSavePipeline.Location = new System.Drawing.Point(432, 464);
            this.btnSavePipeline.Name = "btnSavePipeline";
            this.btnSavePipeline.Size = new System.Drawing.Size(400, 30);
            this.btnSavePipeline.TabIndex = 21;
            this.btnSavePipeline.Text = "Pipeline 순서 저장";
            this.btnSavePipeline.UseVisualStyleBackColor = true;
            this.btnSavePipeline.Click += new System.EventHandler(this.btnSavePipeline_Click);
            // btnSaveStepParams
            this.btnSaveStepParams.Enabled = false;
            this.btnSaveStepParams.Location = new System.Drawing.Point(922, 464);
            this.btnSaveStepParams.Name = "btnSaveStepParams";
            this.btnSaveStepParams.Size = new System.Drawing.Size(530, 30);
            this.btnSaveStepParams.TabIndex = 22;
            this.btnSaveStepParams.Text = "선택 스텝 파라미터 저장";
            this.btnSaveStepParams.UseVisualStyleBackColor = true;
            this.btnSaveStepParams.Click += new System.EventHandler(this.btnSaveStepParams_Click);
            // lblTestSection
            this.lblTestSection.AutoSize = true;
            this.lblTestSection.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Bold);
            this.lblTestSection.Location = new System.Drawing.Point(12, 516);
            this.lblTestSection.Name = "lblTestSection";
            this.lblTestSection.Size = new System.Drawing.Size(538, 15);
            this.lblTestSection.TabIndex = 16;
            this.lblTestSection.Text = "─────────────────── Step 테스트 ───────────────────";
            // btnRunAll
            this.btnRunAll.Enabled = false;
            this.btnRunAll.Location = new System.Drawing.Point(820, 548);
            this.btnRunAll.Name = "btnRunAll";
            this.btnRunAll.Size = new System.Drawing.Size(630, 30);
            this.btnRunAll.TabIndex = 23;
            this.btnRunAll.Text = "▶ 전체 파이프라인 실행";
            this.btnRunAll.UseVisualStyleBackColor = true;
            this.btnRunAll.Click += new System.EventHandler(this.btnRunAll_Click);
            // btnSetTestRegion
            this.btnSetTestRegion.Location = new System.Drawing.Point(820, 588);
            this.btnSetTestRegion.Name = "btnSetTestRegion";
            this.btnSetTestRegion.Size = new System.Drawing.Size(130, 30);
            this.btnSetTestRegion.TabIndex = 11;
            this.btnSetTestRegion.Text = "Region 설정";
            this.btnSetTestRegion.UseVisualStyleBackColor = true;
            this.btnSetTestRegion.Click += new System.EventHandler(this.btnSetTestRegion_Click);
            // btnTestRun
            this.btnTestRun.Location = new System.Drawing.Point(960, 588);
            this.btnTestRun.Name = "btnTestRun";
            this.btnTestRun.Size = new System.Drawing.Size(130, 30);
            this.btnTestRun.TabIndex = 12;
            this.btnTestRun.Text = "스텝 테스트";
            this.btnTestRun.UseVisualStyleBackColor = true;
            this.btnTestRun.Click += new System.EventHandler(this.btnTestRun_Click);
            // txtTestResult
            this.txtTestResult.BackColor = System.Drawing.Color.Black;
            this.txtTestResult.Font = new System.Drawing.Font("Consolas", 9F);
            this.txtTestResult.ForeColor = System.Drawing.Color.Lime;
            this.txtTestResult.Location = new System.Drawing.Point(820, 626);
            this.txtTestResult.Multiline = true;
            this.txtTestResult.Name = "txtTestResult";
            this.txtTestResult.ReadOnly = true;
            this.txtTestResult.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtTestResult.Size = new System.Drawing.Size(630, 218);
            this.txtTestResult.TabIndex = 13;
            // btnOK
            this.btnOK.Location = new System.Drawing.Point(1155, 858);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(120, 35);
            this.btnOK.TabIndex = 14;
            this.btnOK.Text = "확인";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // btnCancel
            this.btnCancel.Location = new System.Drawing.Point(1290, 858);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(120, 35);
            this.btnCancel.TabIndex = 15;
            this.btnCancel.Text = "취소";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // PipelineEditorForm
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1464, 905);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.txtTestResult);
            this.Controls.Add(this.btnTestRun);
            this.Controls.Add(this.btnSetTestRegion);
            this.Controls.Add(this.btnRunAll);
            this.Controls.Add(this.lblTestSection);
            this.Controls.Add(this.btnSaveStepParams);
            this.Controls.Add(this.btnSavePipeline);
            this.Controls.Add(this.grpStepParams);
            this.Controls.Add(this.btnMoveDown);
            this.Controls.Add(this.btnMoveUp);
            this.Controls.Add(this.lstPipeline);
            this.Controls.Add(this.lblPipeline);
            this.Controls.Add(this.txtPipelineName);
            this.Controls.Add(this.lblPipelineName);
            this.Controls.Add(this.btnRemove);
            this.Controls.Add(this.btnAdd);
            this.Controls.Add(this.lstOpenCV);
            this.Controls.Add(this.lblOpenCV);
            this.Controls.Add(this.lstCognex);
            this.Controls.Add(this.lblCognex);
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(1000, 600);
            this.Name = "PipelineEditorForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Pipeline 편집";
            this.Load += new System.EventHandler(this.PipelineEditorForm_Load);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Label    lblCognex;
        private System.Windows.Forms.ListBox  lstCognex;
        private System.Windows.Forms.Label    lblOpenCV;
        private System.Windows.Forms.ListBox  lstOpenCV;
        private System.Windows.Forms.Button   btnAdd;
        private System.Windows.Forms.Button   btnRemove;
        private System.Windows.Forms.Label    lblPipelineName;
        private System.Windows.Forms.TextBox  txtPipelineName;
        private System.Windows.Forms.Label    lblPipeline;
        private System.Windows.Forms.ListBox  lstPipeline;
        private System.Windows.Forms.Button   btnMoveUp;
        private System.Windows.Forms.Button   btnMoveDown;
        private System.Windows.Forms.GroupBox grpStepParams;
        private System.Windows.Forms.Label    lblTestSection;
        private System.Windows.Forms.Button   btnSetTestRegion;
        private System.Windows.Forms.Button   btnTestRun;
        private System.Windows.Forms.TextBox  txtTestResult;
        private System.Windows.Forms.Button   btnOK;
        private System.Windows.Forms.Button   btnCancel;
        private System.Windows.Forms.Button   btnSavePipeline;
        private System.Windows.Forms.Button   btnSaveStepParams;
        private System.Windows.Forms.Button   btnRunAll;
    }
}
