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
            this.lblPipelineMgmt = new System.Windows.Forms.Label();
            this.cmbPipelineSelect = new System.Windows.Forms.ComboBox();
            this.btnNewPl = new System.Windows.Forms.Button();
            this.btnDupePl = new System.Windows.Forms.Button();
            this.btnDeletePl = new System.Windows.Forms.Button();
            this.btnRenamePl = new System.Windows.Forms.Button();
            this.lblCognex = new System.Windows.Forms.Label();
            this.lstCognex = new System.Windows.Forms.ListBox();
            this.lblOpenCV = new System.Windows.Forms.Label();
            this.lstOpenCV = new System.Windows.Forms.ListBox();
            this.lblPipeline = new System.Windows.Forms.Label();
            this.lblStepName = new System.Windows.Forms.Label();
            this.txtStepDisplayName = new System.Windows.Forms.TextBox();
            this.grpStepParams = new System.Windows.Forms.GroupBox();
            this.lblTestSection = new System.Windows.Forms.Label();
            this.btnSaveStepParams = new System.Windows.Forms.Button();
            this.btnSaveAllStepParams = new System.Windows.Forms.Button();
            this.btnRunAll = new System.Windows.Forms.Button();
            this.btnShowAllRegions = new System.Windows.Forms.Button();
            this.btnSingleStepTest = new System.Windows.Forms.Button();
            this.txtTestResult = new System.Windows.Forms.TextBox();
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.lstPipeline = new Vision.UI.DragListBox();
            this.SuspendLayout();
            // 
            // lblPipelineMgmt
            // 
            this.lblPipelineMgmt.AutoSize = true;
            this.lblPipelineMgmt.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Bold);
            this.lblPipelineMgmt.Location = new System.Drawing.Point(12, 12);
            this.lblPipelineMgmt.Name = "lblPipelineMgmt";
            this.lblPipelineMgmt.Size = new System.Drawing.Size(67, 15);
            this.lblPipelineMgmt.TabIndex = 30;
            this.lblPipelineMgmt.Text = "파이프라인";
            // 
            // cmbPipelineSelect
            // 
            this.cmbPipelineSelect.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbPipelineSelect.Font = new System.Drawing.Font("맑은 고딕", 9F);
            this.cmbPipelineSelect.FormattingEnabled = true;
            this.cmbPipelineSelect.Location = new System.Drawing.Point(12, 32);
            this.cmbPipelineSelect.Name = "cmbPipelineSelect";
            this.cmbPipelineSelect.Size = new System.Drawing.Size(300, 23);
            this.cmbPipelineSelect.TabIndex = 31;
            this.cmbPipelineSelect.SelectedIndexChanged += new System.EventHandler(this.cmbPipelineSelect_SelectedIndexChanged);
            // 
            // btnNewPl
            // 
            this.btnNewPl.Location = new System.Drawing.Point(12, 59);
            this.btnNewPl.Name = "btnNewPl";
            this.btnNewPl.Size = new System.Drawing.Size(64, 24);
            this.btnNewPl.TabIndex = 32;
            this.btnNewPl.Text = "신규";
            this.btnNewPl.UseVisualStyleBackColor = true;
            this.btnNewPl.Click += new System.EventHandler(this.btnNewPl_Click);
            // 
            // btnDupePl
            // 
            this.btnDupePl.Location = new System.Drawing.Point(80, 59);
            this.btnDupePl.Name = "btnDupePl";
            this.btnDupePl.Size = new System.Drawing.Size(64, 24);
            this.btnDupePl.TabIndex = 33;
            this.btnDupePl.Text = "복제";
            this.btnDupePl.UseVisualStyleBackColor = true;
            this.btnDupePl.Click += new System.EventHandler(this.btnDupePl_Click);
            // 
            // btnDeletePl
            // 
            this.btnDeletePl.Location = new System.Drawing.Point(148, 59);
            this.btnDeletePl.Name = "btnDeletePl";
            this.btnDeletePl.Size = new System.Drawing.Size(64, 24);
            this.btnDeletePl.TabIndex = 34;
            this.btnDeletePl.Text = "삭제";
            this.btnDeletePl.UseVisualStyleBackColor = true;
            this.btnDeletePl.Click += new System.EventHandler(this.btnDeletePl_Click);
            // 
            // btnRenamePl
            // 
            this.btnRenamePl.Location = new System.Drawing.Point(216, 59);
            this.btnRenamePl.Name = "btnRenamePl";
            this.btnRenamePl.Size = new System.Drawing.Size(96, 24);
            this.btnRenamePl.TabIndex = 35;
            this.btnRenamePl.Text = "이름 변경";
            this.btnRenamePl.UseVisualStyleBackColor = true;
            this.btnRenamePl.Click += new System.EventHandler(this.btnRenamePl_Click);
            // 
            // lblCognex
            // 
            this.lblCognex.AutoSize = true;
            this.lblCognex.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Bold);
            this.lblCognex.Location = new System.Drawing.Point(12, 96);
            this.lblCognex.Name = "lblCognex";
            this.lblCognex.Size = new System.Drawing.Size(78, 15);
            this.lblCognex.TabIndex = 20;
            this.lblCognex.Text = "Cognex 스텝";
            // 
            // lstCognex
            // 
            this.lstCognex.Font = new System.Drawing.Font("Consolas", 9.5F);
            this.lstCognex.FormattingEnabled = true;
            this.lstCognex.HorizontalScrollbar = true;
            this.lstCognex.ItemHeight = 15;
            this.lstCognex.Location = new System.Drawing.Point(12, 116);
            this.lstCognex.Name = "lstCognex";
            this.lstCognex.Size = new System.Drawing.Size(461, 184);
            this.lstCognex.TabIndex = 0;
            this.lstCognex.DoubleClick += new System.EventHandler(this.lstCognex_DoubleClick);
            // 
            // lblOpenCV
            // 
            this.lblOpenCV.AutoSize = true;
            this.lblOpenCV.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Bold);
            this.lblOpenCV.Location = new System.Drawing.Point(12, 328);
            this.lblOpenCV.Name = "lblOpenCV";
            this.lblOpenCV.Size = new System.Drawing.Size(82, 15);
            this.lblOpenCV.TabIndex = 19;
            this.lblOpenCV.Text = "OpenCV 스텝";
            // 
            // lstOpenCV
            // 
            this.lstOpenCV.Font = new System.Drawing.Font("Consolas", 9.5F);
            this.lstOpenCV.FormattingEnabled = true;
            this.lstOpenCV.HorizontalScrollbar = true;
            this.lstOpenCV.ItemHeight = 15;
            this.lstOpenCV.Location = new System.Drawing.Point(12, 348);
            this.lstOpenCV.Name = "lstOpenCV";
            this.lstOpenCV.Size = new System.Drawing.Size(461, 154);
            this.lstOpenCV.TabIndex = 1;
            this.lstOpenCV.DoubleClick += new System.EventHandler(this.lstOpenCV_DoubleClick);
            // 
            // lblPipeline
            // 
            this.lblPipeline.AutoSize = true;
            this.lblPipeline.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Bold);
            this.lblPipeline.Location = new System.Drawing.Point(498, 40);
            this.lblPipeline.Name = "lblPipeline";
            this.lblPipeline.Size = new System.Drawing.Size(95, 15);
            this.lblPipeline.TabIndex = 17;
            this.lblPipeline.Text = "파이프라인 순서";
            // 
            // lblStepName
            // 
            this.lblStepName.AutoSize = true;
            this.lblStepName.Location = new System.Drawing.Point(1074, 16);
            this.lblStepName.Name = "lblStepName";
            this.lblStepName.Size = new System.Drawing.Size(33, 12);
            this.lblStepName.TabIndex = 51;
            this.lblStepName.Text = "명칭:";
            // 
            // txtStepDisplayName
            // 
            this.txtStepDisplayName.Enabled = false;
            this.txtStepDisplayName.Location = new System.Drawing.Point(1112, 12);
            this.txtStepDisplayName.Name = "txtStepDisplayName";
            this.txtStepDisplayName.Size = new System.Drawing.Size(340, 21);
            this.txtStepDisplayName.TabIndex = 50;
            // 
            // grpStepParams
            // 
            this.grpStepParams.Font = new System.Drawing.Font("맑은 고딕", 9F);
            this.grpStepParams.Location = new System.Drawing.Point(953, 42);
            this.grpStepParams.Name = "grpStepParams";
            this.grpStepParams.Size = new System.Drawing.Size(499, 416);
            this.grpStepParams.TabIndex = 8;
            this.grpStepParams.TabStop = false;
            this.grpStepParams.Text = "스텝 파라미터";
            // 
            // lblTestSection
            // 
            this.lblTestSection.AutoSize = true;
            this.lblTestSection.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Bold);
            this.lblTestSection.Location = new System.Drawing.Point(9, 527);
            this.lblTestSection.Name = "lblTestSection";
            this.lblTestSection.Size = new System.Drawing.Size(538, 15);
            this.lblTestSection.TabIndex = 16;
            this.lblTestSection.Text = "─────────────────── Step 테스트 ───────────────────";
            // 
            // btnSaveStepParams
            // 
            this.btnSaveStepParams.Enabled = false;
            this.btnSaveStepParams.Location = new System.Drawing.Point(501, 464);
            this.btnSaveStepParams.Name = "btnSaveStepParams";
            this.btnSaveStepParams.Size = new System.Drawing.Size(951, 28);
            this.btnSaveStepParams.TabIndex = 22;
            this.btnSaveStepParams.Text = "선택 스텝 파라미터 저장";
            this.btnSaveStepParams.UseVisualStyleBackColor = true;
            this.btnSaveStepParams.Click += new System.EventHandler(this.btnSaveStepParams_Click);
            // 
            // btnSaveAllStepParams
            // 
            this.btnSaveAllStepParams.Location = new System.Drawing.Point(501, 496);
            this.btnSaveAllStepParams.Name = "btnSaveAllStepParams";
            this.btnSaveAllStepParams.Size = new System.Drawing.Size(951, 28);
            this.btnSaveAllStepParams.TabIndex = 24;
            this.btnSaveAllStepParams.Text = "전체 스텝 파라미터 저장";
            this.btnSaveAllStepParams.UseVisualStyleBackColor = true;
            this.btnSaveAllStepParams.Click += new System.EventHandler(this.btnSaveAllStepParams_Click);
            // 
            // btnRunAll
            // 
            this.btnRunAll.Enabled = false;
            this.btnRunAll.Location = new System.Drawing.Point(822, 592);
            this.btnRunAll.Name = "btnRunAll";
            this.btnRunAll.Size = new System.Drawing.Size(630, 30);
            this.btnRunAll.TabIndex = 23;
            this.btnRunAll.Text = "▶ 전체 파이프라인 실행";
            this.btnRunAll.UseVisualStyleBackColor = true;
            this.btnRunAll.Click += new System.EventHandler(this.btnRunAll_Click);
            // 
            // btnShowAllRegions
            // 
            this.btnShowAllRegions.Location = new System.Drawing.Point(822, 556);
            this.btnShowAllRegions.Name = "btnShowAllRegions";
            this.btnShowAllRegions.Size = new System.Drawing.Size(630, 30);
            this.btnShowAllRegions.TabIndex = 11;
            this.btnShowAllRegions.Text = "전체 Region 보기";
            this.btnShowAllRegions.UseVisualStyleBackColor = true;
            this.btnShowAllRegions.Click += new System.EventHandler(this.btnShowAllRegions_Click);
            // 
            // btnSingleStepTest
            // 
            this.btnSingleStepTest.Enabled = false;
            this.btnSingleStepTest.Location = new System.Drawing.Point(822, 628);
            this.btnSingleStepTest.Name = "btnSingleStepTest";
            this.btnSingleStepTest.Size = new System.Drawing.Size(630, 30);
            this.btnSingleStepTest.TabIndex = 12;
            this.btnSingleStepTest.Text = "▶ 단일 스탭 테스트";
            this.btnSingleStepTest.UseVisualStyleBackColor = true;
            this.btnSingleStepTest.Click += new System.EventHandler(this.btnSingleStepTest_Click);
            // 
            // txtTestResult
            // 
            this.txtTestResult.BackColor = System.Drawing.Color.Black;
            this.txtTestResult.Font = new System.Drawing.Font("Consolas", 9F);
            this.txtTestResult.ForeColor = System.Drawing.Color.Lime;
            this.txtTestResult.Location = new System.Drawing.Point(820, 668);
            this.txtTestResult.Multiline = true;
            this.txtTestResult.Name = "txtTestResult";
            this.txtTestResult.ReadOnly = true;
            this.txtTestResult.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtTestResult.Size = new System.Drawing.Size(630, 182);
            this.txtTestResult.TabIndex = 13;
            // 
            // btnOK
            // 
            this.btnOK.Location = new System.Drawing.Point(1155, 858);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(120, 35);
            this.btnOK.TabIndex = 14;
            this.btnOK.Text = "확인";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Location = new System.Drawing.Point(1290, 858);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(120, 35);
            this.btnCancel.TabIndex = 15;
            this.btnCancel.Text = "취소";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // lstPipeline
            // 
            this.lstPipeline.AllowDrop = true;
            this.lstPipeline.Font = new System.Drawing.Font("Consolas", 9.5F);
            this.lstPipeline.FormattingEnabled = true;
            this.lstPipeline.HorizontalScrollbar = true;
            this.lstPipeline.InsertIndex = -1;
            this.lstPipeline.ItemHeight = 15;
            this.lstPipeline.Location = new System.Drawing.Point(501, 62);
            this.lstPipeline.Name = "lstPipeline";
            this.lstPipeline.Size = new System.Drawing.Size(446, 394);
            this.lstPipeline.TabIndex = 5;
            this.lstPipeline.SelectedIndexChanged += new System.EventHandler(this.lstPipeline_SelectedIndexChanged);
            this.lstPipeline.DragDrop += new System.Windows.Forms.DragEventHandler(this.lstPipeline_DragDrop);
            this.lstPipeline.DragOver += new System.Windows.Forms.DragEventHandler(this.lstPipeline_DragOver);
            this.lstPipeline.DragLeave += new System.EventHandler(this.lstPipeline_DragLeave);
            this.lstPipeline.DoubleClick += new System.EventHandler(this.lstPipeline_DoubleClick);
            this.lstPipeline.MouseDown += new System.Windows.Forms.MouseEventHandler(this.lstPipeline_MouseDown);
            this.lstPipeline.MouseMove += new System.Windows.Forms.MouseEventHandler(this.lstPipeline_MouseMove);
            this.lstPipeline.MouseUp += new System.Windows.Forms.MouseEventHandler(this.lstPipeline_MouseUp);
            // 
            // PipelineEditorForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1464, 905);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.txtTestResult);
            this.Controls.Add(this.btnSingleStepTest);
            this.Controls.Add(this.btnShowAllRegions);
            this.Controls.Add(this.btnRunAll);
            this.Controls.Add(this.lblTestSection);
            this.Controls.Add(this.txtStepDisplayName);
            this.Controls.Add(this.lblStepName);
            this.Controls.Add(this.btnSaveAllStepParams);
            this.Controls.Add(this.btnSaveStepParams);
            this.Controls.Add(this.grpStepParams);
            this.Controls.Add(this.lstPipeline);
            this.Controls.Add(this.lblPipeline);
            this.Controls.Add(this.lstOpenCV);
            this.Controls.Add(this.lblOpenCV);
            this.Controls.Add(this.lstCognex);
            this.Controls.Add(this.lblCognex);
            this.Controls.Add(this.btnRenamePl);
            this.Controls.Add(this.btnDeletePl);
            this.Controls.Add(this.btnDupePl);
            this.Controls.Add(this.btnNewPl);
            this.Controls.Add(this.cmbPipelineSelect);
            this.Controls.Add(this.lblPipelineMgmt);
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

        private System.Windows.Forms.Label    lblPipelineMgmt;
        private System.Windows.Forms.ComboBox cmbPipelineSelect;
        private System.Windows.Forms.Button   btnNewPl;
        private System.Windows.Forms.Button   btnDupePl;
        private System.Windows.Forms.Button   btnDeletePl;
        private System.Windows.Forms.Button   btnRenamePl;
        private System.Windows.Forms.Label    lblCognex;
        private System.Windows.Forms.ListBox  lstCognex;
        private System.Windows.Forms.Label    lblOpenCV;
        private System.Windows.Forms.ListBox  lstOpenCV;
        private System.Windows.Forms.Label    lblPipeline;
        private Vision.UI.DragListBox         lstPipeline;
        private System.Windows.Forms.GroupBox grpStepParams;
        private System.Windows.Forms.Label   lblStepName;
        private System.Windows.Forms.TextBox txtStepDisplayName;
        private System.Windows.Forms.Label    lblTestSection;
        private System.Windows.Forms.Button   btnShowAllRegions;
        private System.Windows.Forms.Button   btnSingleStepTest;
        private System.Windows.Forms.TextBox  txtTestResult;
        private System.Windows.Forms.Button   btnOK;
        private System.Windows.Forms.Button   btnCancel;
        private System.Windows.Forms.Button   btnSaveStepParams;
        private System.Windows.Forms.Button   btnSaveAllStepParams;
        private System.Windows.Forms.Button   btnRunAll;
    }
}
