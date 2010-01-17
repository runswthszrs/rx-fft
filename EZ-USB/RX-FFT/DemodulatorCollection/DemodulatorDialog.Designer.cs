﻿namespace DemodulatorCollection
{
    partial class DemodulatorDialog
    {
        /// <summary>
        /// Erforderliche Designervariable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Verwendete Ressourcen bereinigen.
        /// </summary>
        /// <param name="disposing">True, wenn verwaltete Ressourcen gelöscht werden sollen; andernfalls False.</param>
        protected override void Dispose(bool disposing)
        {
            CloseDemod();
            CloseSource();

            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Vom Windows Form-Designer generierter Code

        /// <summary>
        /// Erforderliche Methode für die Designerunterstützung.
        /// Der Inhalt der Methode darf nicht mit dem Code-Editor geändert werden.
        /// </summary>
        private void InitializeComponent()
        {
            this.btnOpenPulseDelay = new System.Windows.Forms.Button();
            this.btnOpenPulseKeying = new System.Windows.Forms.Button();
            this.txtPulseDelay = new System.Windows.Forms.TextBox();
            this.txtPulseKeying = new System.Windows.Forms.TextBox();
            this.btnOpenASK = new System.Windows.Forms.Button();
            this.txtAmplitudeShiftKeying = new System.Windows.Forms.TextBox();
            this.btnOpen = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // btnOpenPulseDelay
            // 
            this.btnOpenPulseDelay.Location = new System.Drawing.Point(12, 50);
            this.btnOpenPulseDelay.Name = "btnOpenPulseDelay";
            this.btnOpenPulseDelay.Size = new System.Drawing.Size(79, 23);
            this.btnOpenPulseDelay.TabIndex = 0;
            this.btnOpenPulseDelay.Text = "Pulse Delay";
            this.btnOpenPulseDelay.UseVisualStyleBackColor = true;
            this.btnOpenPulseDelay.Click += new System.EventHandler(this.btnOpenPulseDelay_Click);
            // 
            // btnOpenPulseKeying
            // 
            this.btnOpenPulseKeying.Location = new System.Drawing.Point(12, 160);
            this.btnOpenPulseKeying.Name = "btnOpenPulseKeying";
            this.btnOpenPulseKeying.Size = new System.Drawing.Size(79, 23);
            this.btnOpenPulseKeying.TabIndex = 0;
            this.btnOpenPulseKeying.Text = "Pulse Keying";
            this.btnOpenPulseKeying.UseVisualStyleBackColor = true;
            this.btnOpenPulseKeying.Click += new System.EventHandler(this.btnOpenPulseKeying_Click);
            // 
            // txtPulseDelay
            // 
            this.txtPulseDelay.Font = new System.Drawing.Font("Courier New", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtPulseDelay.Location = new System.Drawing.Point(99, 50);
            this.txtPulseDelay.Multiline = true;
            this.txtPulseDelay.Name = "txtPulseDelay";
            this.txtPulseDelay.ReadOnly = true;
            this.txtPulseDelay.Size = new System.Drawing.Size(476, 104);
            this.txtPulseDelay.TabIndex = 1;
            // 
            // txtPulseKeying
            // 
            this.txtPulseKeying.Font = new System.Drawing.Font("Courier New", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtPulseKeying.Location = new System.Drawing.Point(99, 160);
            this.txtPulseKeying.Multiline = true;
            this.txtPulseKeying.Name = "txtPulseKeying";
            this.txtPulseKeying.ReadOnly = true;
            this.txtPulseKeying.Size = new System.Drawing.Size(476, 104);
            this.txtPulseKeying.TabIndex = 1;
            // 
            // btnOpenASK
            // 
            this.btnOpenASK.Location = new System.Drawing.Point(12, 270);
            this.btnOpenASK.Name = "btnOpenASK";
            this.btnOpenASK.Size = new System.Drawing.Size(79, 23);
            this.btnOpenASK.TabIndex = 2;
            this.btnOpenASK.Text = "ASK";
            this.btnOpenASK.UseVisualStyleBackColor = true;
            this.btnOpenASK.Click += new System.EventHandler(this.btnOpenASK_Click);
            // 
            // txtAmplitudeShiftKeying
            // 
            this.txtAmplitudeShiftKeying.Font = new System.Drawing.Font("Courier New", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtAmplitudeShiftKeying.Location = new System.Drawing.Point(99, 270);
            this.txtAmplitudeShiftKeying.Multiline = true;
            this.txtAmplitudeShiftKeying.Name = "txtAmplitudeShiftKeying";
            this.txtAmplitudeShiftKeying.ReadOnly = true;
            this.txtAmplitudeShiftKeying.Size = new System.Drawing.Size(476, 104);
            this.txtAmplitudeShiftKeying.TabIndex = 1;
            // 
            // btnOpen
            // 
            this.btnOpen.Location = new System.Drawing.Point(12, 12);
            this.btnOpen.Name = "btnOpen";
            this.btnOpen.Size = new System.Drawing.Size(75, 23);
            this.btnOpen.TabIndex = 3;
            this.btnOpen.Text = "Open";
            this.btnOpen.UseVisualStyleBackColor = true;
            this.btnOpen.Click += new System.EventHandler(this.btnOpen_Click);
            // 
            // DemodulatorDialog
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(634, 407);
            this.Controls.Add(this.btnOpen);
            this.Controls.Add(this.btnOpenASK);
            this.Controls.Add(this.txtAmplitudeShiftKeying);
            this.Controls.Add(this.txtPulseKeying);
            this.Controls.Add(this.txtPulseDelay);
            this.Controls.Add(this.btnOpenPulseKeying);
            this.Controls.Add(this.btnOpenPulseDelay);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Name = "DemodulatorDialog";
            this.Text = "Demodulators";
            this.ResumeLayout(false);
            this.PerformLayout();

        }


        #endregion

        private System.Windows.Forms.Button btnOpenPulseDelay;
        private System.Windows.Forms.Button btnOpenPulseKeying;
        private System.Windows.Forms.TextBox txtPulseDelay;
        private System.Windows.Forms.TextBox txtPulseKeying;
        private System.Windows.Forms.Button btnOpenASK;
        private System.Windows.Forms.TextBox txtAmplitudeShiftKeying;
        private System.Windows.Forms.Button btnOpen;
    }
}

