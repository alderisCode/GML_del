/*
 * Created by SharpDevelop.
 * User: user
 * Date: 05.03.2019
 * Time: 11:48
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Windows.Forms;
using System.IO;

namespace GML_del
{
	/// <summary>
	/// Description of MainForm.
	/// </summary>
	public partial class MainForm : Form
	{
		int LinesCount = 0; 	// ilość linii w pliku GML
		int ObCount = 0;		// liczba obiektów w pliku
		Collection<ObjectInfo> Objects;
		Collection<ObjectType> ObjTypes;
		Collection<string> LokalneId;
		
		public MainForm()
		{
			InitializeComponent();
			this.AllowDrop = true;
			this.DragEnter += new DragEventHandler(Form1_DragEnter);
			this.DragDrop += new DragEventHandler(Form1_DragDrop);
			Objects = new Collection<ObjectInfo>();
			ObjTypes = new Collection<ObjectType>();
			LokalneId = new Collection<string>();			// lista lokalnychId do szybkiego przeszukiwania
			// ---			
			lbInfo.Text = "";
		}


		void Form1_DragEnter(object sender, DragEventArgs e)
		{
			if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
		}

		void Form1_DragDrop(object sender, DragEventArgs e)
		{
			string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
			//foreach (string file in files) Console.WriteLine(file);
			OpenFile(files[0]);
		}

		void Button1Click(object sender, EventArgs e)
		{
			progressBar1.Visible = false;
			if (openFileDialog1.ShowDialog() == DialogResult.OK) 
			{
				OpenFile(openFileDialog1.FileName);
				
			}
		}
		
		void OpenFile(string fName)
        {
			if (richTextBox1.TextLength > 0)
			{
				Log("\n\n-----\n", Color.WhiteSmoke);
			}
			textBox1.Text = fName;
			Objects.Clear();
			LokalneId.Clear();
			ObjTypes.Clear();
			dataGridView1.Rows.Clear();
			dataGridView2.Rows.Clear();
			AnalizujPlik(fName);
		}

		void AnalizujPlik(string path) 
		{
			Log("Otwieram Plik: " + path);
			lbInfo.Text = "Analizuję plik. Proszę czekać...";
			LinesCount = 0;
			ObCount = 0;
			var oi = new ObjectInfo();
			bool GmlMember = false;			
			Log("\nCzytam obiekty z pliku...");
			Application.DoEvents();
			using (FileStream fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
			using (BufferedStream bs = new BufferedStream(fs))
			using (StreamReader sr = new StreamReader(bs))			
			{
    			string line;
    			while ((line = sr.ReadLine()) != null)
    			{
    				LinesCount++;
    				if (line.Contains("</gml:featureMember>")) 
    				{
    					oi.LineEnd = LinesCount;
    					Objects.Add(oi);
    				}
    				else if (line.Contains("<gml:featureMember>")) 
    				{ 
    					GmlMember = true;
    					ObCount++;
    					oi = new ObjectInfo();
//    					oi.LineNr = LinesCount+1;
    					oi.LineStart = LinesCount;
    				}
    				
    				if (GmlMember && line.Contains("<bt:KR_ObiektKarto"))
    				{
    					oi.ObKarto = true;
    					oi.Type = "KR_ObiektKarto";
    				}
    				
   					string S = line.Trim();
    				if (GmlMember) 
    				{    					
    					if (S.Contains("<ges:GES_") || S.Contains("<bdz:BDZ_") || S.Contains("<egb:EGB_"))
    					{
    						GmlMember = false;
    						int i = S.IndexOf(':');
    						int j = S.IndexOf(' ');
    						oi.Type = S.Substring(i+1, j-i-1);
							AddObjType(oi.Type);
    					}
    				}

    				if (S.Contains("<bt:lokalnyId>"))
    				{
    					if (oi.ObKarto) 
    						{
								oi.references.Add(new RefInfo(LinesCount+1, GetXMLValue(S), true));
								oi.lokalnyId = "ref: " + GetXMLValue(S);								
						}
    					else
    						{ 
								oi.lokalnyId = GetXMLValue(S);
								LokalneId.Add(oi.lokalnyId);
						}
    				}

					if (S.Contains("xlink:href"))
					{
						string S2 = GetXlinkID(S);
						oi.references.Add(new RefInfo(LinesCount+1, S2, false));
					}

					if (S.Contains(":startObiekt>"))
    				{
    					oi.StartOb = Convert.ToDateTime(GetXMLValue(S));
    				}
    				if (S.Contains(":poczatekWersjiObiektu") || S.Contains(":startWersjaObiekt")) 
    				{
    					oi.StartWersjiOb = Convert.ToDateTime(GetXMLValue(S));
    				}
    				if (S.Contains(":koniecWersjiObiektu") || S.Contains(":koniecWersjaObiekt")) 
    				{
    					oi.KoniecWersjiOb = Convert.ToDateTime(GetXMLValue(S));
						if (!oi.Deleted) {
							oi.Archived = true;
                        }
						
    				}
    				if (S.Contains(":koniecObiekt")) 
    				{
    					oi.KoniecOb = Convert.ToDateTime(GetXMLValue(S));
						oi.Deleted = true;
						oi.Archived = false;
    				}
    				    				
    					
    			}
			}	
			Log("Ok.\nZnaleziono " + ObCount.ToString() + " ob.");
			Log("\nWczytuję do tabeli... ");
			FileInfo fi = new FileInfo(path);
			string fSize = (Math.Round((double)fi.Length / 1024 / 1024, 3)).ToString() + " MB";

			lbInfo.Text = fSize + ",   " + Convert.ToString(LinesCount) + " linii,   " +
				Convert.ToString(ObCount) + " obiektów";			
			Application.DoEvents();
			
			dataGridView1.SuspendLayout();			
			string txt;
			foreach (ObjectInfo ObI in Objects) 
			{
			
				// if (ObI.ObKarto) { continue; }
				txt = String.Concat(ObI.LineStart.ToString(), "-", ObI.LineEnd.ToString() , ";", ObI.Type, ";", ObI.lokalnyId, ";",
									Date2Str(ObI.StartOb), ";", Date2Str(ObI.StartWersjiOb), ";", Date2Str(ObI.KoniecWersjiOb), ";", Date2Str(ObI.KoniecOb), ";", " " );
				dataGridView1.Rows.Add(txt.Split(';'));
			}
			dataGridView1.ResumeLayout();
		
			statusLabel.Text = String.Concat("Znaleziono ", LinesCount.ToString(), " linii, ", ObCount.ToString(), " obiektów. Gotowe.");
			Log("Gotowe.\n");

			// Typy obiektów
			foreach (ObjectType ot in ObjTypes)
			{
				string S = String.Concat("false;", ot.Type, ";", ot.Count.ToString());
				dataGridView2.Rows.Add(S.Split(';'));
			}

			// Szukanie obiektów archiwalnych
			FindArchObj();
		}
	

		string GetXMLValue(string txt) 
		{
			int i = txt.IndexOf('>');			
			return txt.Substring(i+1, txt.Length-(2*i+3));
		}


		void Log(string txt) 
		{
			richTextBox1.AppendText(txt);
			richTextBox1.ScrollToCaret();
			Application.DoEvents();
		}

		void Log(string txt, Color color) 
		{
			richTextBox1.SelectionStart = richTextBox1.TextLength;
			richTextBox1.SelectionLength = 0;			
			richTextBox1.SelectionColor = color;
			richTextBox1.AppendText(txt);
			richTextBox1.ScrollToCaret();
			richTextBox1.SelectionColor = richTextBox1.ForeColor;
			Application.DoEvents();
		}
		
		
		string Date2Str(DateTime dt) 
		{
			if (dt.Year < 2 && dt.Month < 2 && dt.Day < 2) 
			  { return "-"; }
			else 
			  { return dt.ToString(); }
		}
		
		void PBValue(int V, int Max)
        {
			progressBar1.Value = V;
			progressBar1.Refresh();
			lbProgress.Text = String.Concat(V.ToString(), " / ", Max.ToString());
			lbProgress.Refresh();
			Application.DoEvents();
			//Log("\n" + V.ToString());
        }

		void FindArchObj() 
		{
			Log("\nSprawdzam status obiektów... ");
			progressBar1.Maximum = Objects.Count;
			progressBar1.Value = 0;
			if (!cbSilentMode.Checked)
			{
				progressBar1.Visible = true;
				lbProgress.Visible = true;
			}
			int archCount = 0;
			int delCount = 0;
			dataGridView1.SuspendLayout();              // <---
			DataGridViewCellStyle style1 = new DataGridViewCellStyle(this.dataGridView1.RowsDefaultCellStyle);
			style1.ForeColor = Color.DarkRed;
			style1.BackColor = Color.LightCoral;
			DataGridViewCellStyle style2 = new DataGridViewCellStyle(this.dataGridView1.RowsDefaultCellStyle);
			style2.ForeColor = Color.SaddleBrown;
			style2.BackColor = Color.LightGray;
			for (int i = 0; i < Objects.Count - 1; i++)
			{
				if (!cbSilentMode.Checked)
				PBValue(i, progressBar1.Maximum);

				// jeśli koniec obiektu
				if (Objects[i].Deleted)
				{
					// oznacz jako obiekt usunięty
					delCount++;
					if (!cbSilentMode.Checked)
					{
						dataGridView1.Rows[i].Cells["StatusOb"].Style = style1;
						dataGridView1.Rows[i].Cells["StatusOb"].Value = "Usunięty";
					}
				}
				// lub jeśli koniec wersji - ob. archiwalny
				else if (Objects[i].Archived && !Objects[i].Deleted)
				{
					// oznacz jako obiekt archiwalny
					archCount++;
					if (!cbSilentMode.Checked)
					{
						dataGridView1.Rows[i].Cells["StatusOb"].Style = style2;
						dataGridView1.Rows[i].Cells["StatusOb"].Value = "Archiwalny";
					}
				}
			}
			
			dataGridView1.ResumeLayout();

			if (archCount == 0)
			{
				Log("\nNie znaleziono obiektów archiwalnych.", Color.LightSkyBlue);
			}
			else
			{
				Log("\nZnaleziono ", Color.OrangeRed);
				Log(archCount.ToString(), Color.OrangeRed);
				Log(" ob. arch.", Color.OrangeRed);
			}
			if (delCount == 0)
			{
				Log("\nNie znaleziono obiektów usuniętych.", Color.LightSkyBlue);
			}
			else
			{
				Log("\nZnaleziono ", Color.LightCoral);
				Log(delCount.ToString(), Color.LightCoral);
				Log(" ob. usun.", Color.LightCoral);
			}
			progressBar1.Visible = false;
			lbProgress.Visible = false;
		}
		
		
		// Zamknięcie programu
		void Button2Click(object sender, EventArgs e)
		{
			this.Close();
		}
		
		
		class LinesBlock 
		{
			public long lnFrom;
			public long lnTo;
			
			public LinesBlock(long lFrom, long lTo)
			{
				lnFrom = lFrom;
				lnTo = lTo;
			}
		}
		
		void Button4Click(object sender, EventArgs e)
		{
			// usunięcie ob. archiwalnych
			// lista linii od których pominięcie
			Log("\n\nZapis obiektów do nowego pliku.");
			Collection<string> obTypes = new Collection<string>();
			// lista typów obiektów do usunięcia
			for (int i=0; i<ObjTypes.Count; i++)
            {
				if (Convert.ToBoolean(dataGridView2.Rows[i].Cells[0].Value))
                {
					obTypes.Add(ObjTypes[i].Type);
                }
            }
			// bloki linii w pliku do pominięcia
			Log("\nOznaczanie linii do pominięcia...");
			Collection<LinesBlock> blocks = new Collection<LinesBlock>();
			Collection<string> locIdToDelete = new Collection<string>();
			progressBar1.Maximum = ObCount;
			progressBar1.Value = 0;
			progressBar1.Visible = true;
			lbProgress.Visible = true; 
			int obc = 0;
			foreach (ObjectInfo o in Objects) 
			{
				obc++;
				if ((obc / 100) - Math.Truncate((double)(obc / 100)) == 0)
				{
					// co setny obiekt...
					PBValue(obc, ObCount);
				}
				if (checkBox2.Checked)   // pojedyncze akcje z comboBoxa
                {
					switch (comboBox1.SelectedIndex)
                    {
						case 0:                                 // tylko ob. usunięte
							if (!o.Deleted)
							{
								o.ToRemove = true;
								blocks.Add(new LinesBlock(o.LineStart, o.LineEnd));
							}
							break;
						default:
							break;
                    }
					continue;
                }
				 
				
				if ((o.Archived && checkBox3.Checked) || (obTypes.IndexOf(o.Type)>-1))
				{
					blocks.Add(new LinesBlock(o.LineStart, o.LineEnd));
					if (!o.ObKarto) locIdToDelete.Add(o.lokalnyId);
					o.ToRemove = true;
				}
				
				if ((o.ObKarto) && (checkBox1.Checked) && (o.references.Count > 0))
                {
					//Log("\nKarto: " + o.lokalnyId, Color.LightSteelBlue);
					if (LokalneId.IndexOf(o.references[0].lokalnyId) < 0)
					{
						//Log("\nKarto: "+o.references[0].lokalnyId, Color.LightSteelBlue);
						blocks.Add(new LinesBlock(o.LineStart, o.LineEnd));
						o.ToRemove = true;
					}
                }
			}

			// usuwanie obiektów karto z referencją do usuwanych obiektów
			// oraz pomijanych typów obiektów
			Log("OK \nOznaczanie ob. karto z relacją do usuwanych obiektów...");
			progressBar1.Maximum = ObCount;
			progressBar1.Value = 0;
			progressBar1.Visible = true;
			lbProgress.Visible = true; 
			obc = 0;
			foreach (ObjectInfo o in Objects)
			{
				obc++;
				if ((obc / 100) - Math.Truncate((double)(obc / 100)) == 0)
				{
					// co setny obiekt...
					PBValue(obc, ObCount);
				}
				if ((o.ObKarto) && (!o.ToRemove))
				{
					if (locIdToDelete.IndexOf(o.references[0].lokalnyId) > -1)
					{
						blocks.Add(new LinesBlock(o.LineStart, o.LineEnd));
						o.ToRemove = true;
					}
				}
			}
			// nazwa nowego pliku
			string path = textBox1.Text;
			string newFileName = Path.GetFileNameWithoutExtension(textBox1.Text) + textBox2.Text;
			string dir = Path.GetDirectoryName(path);
			string ext = Path.GetExtension(path);
			path =  Path.Combine(dir, newFileName + ext);
			Log(" OK\nNowy plik:\n  " + newFileName + ext);

			// zapis	
			Log("\n\nZapisywanie...");
			progressBar1.Maximum = (int)LinesCount;
			progressBar1.Value = 0;
			progressBar1.Visible = true;
			lbProgress.Visible = true;
			int linesBlockIdx = 0; 
			bool lnOk;
			using (var writer = new StreamWriter(path))
			{
				long lnCount = 0;
				foreach (var line in File.ReadLines(textBox1.Text))
				{
					lnOk = true;
					PBValue((int)lnCount, progressBar1.Maximum);

					if (lnCount < blocks[linesBlockIdx].lnFrom) 
						{ lnOk = true; }
					else if (lnCount >= blocks[linesBlockIdx].lnFrom && lnCount < blocks[linesBlockIdx].lnTo)
						{ lnOk = false; }
					else if (lnCount == blocks[linesBlockIdx].lnTo)
						{
							lnOk = false;
							if (linesBlockIdx<blocks.Count-1) { linesBlockIdx++; }
						}
					else { lnOk = true; }
					lnCount++;
					// jeśli bieżąca linia jest na liście
					//foreach (LinesBlock lb in blocks) 
					//{
					//	if (lnCount >= lb.lnFrom && lnCount <= lb.lnTo) { lnOk = false; }
					//}
				
					if (lnOk) 
					{ 
						writer.WriteLine(line); 
					}
				}
			}
			Log("\nGotowe.");
			progressBar1.Visible = false;
			lbProgress.Visible = false;


			// zapis 2


		}

		void AddObjType(string ot)
        {
			for (int i = 0; i < ObjTypes.Count; i++)
            {
				if (ObjTypes[i].Type == ot)
                {
					ObjTypes[i].Count++;
					return;
                }
            }
			//MessageBox.Show("Nowy typ:\n" + ot);
			ObjTypes.Add(new ObjectType(ot, 1));
			return;
		}


		void Button3Click(object sender, EventArgs e)
		{
			MessageBox.Show("GML-DEL\nProgram do czyszczenia plików GML.\n\n(C) 2022 Starostwo Powiatowe w Opolu\nFreeware", "O programie");
		}

        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {
			comboBox1.Enabled = checkBox2.Checked;
			checkBox1.Enabled = !checkBox2.Checked;
			checkBox3.Enabled = !checkBox2.Checked;
		}


		string GetXlinkID(string txt)
        {
			string s = "";
			string[] sList = txt.Split('"');
			bool xlink = false;
			foreach (string t in sList)
            {
				if (xlink)
                {
					s = t;
					break;
                }
				if (t.IndexOf("xlink:href") >= 0) xlink = true;
            }
			if (s.IndexOf(':')>=0)
            {
				sList = s.Split(':');
				s = sList[sList.Length - 1];
            }
			return s;
        }

        private void cbSilentMode_CheckedChanged(object sender, EventArgs e)
        {
			if (cbSilentMode.Checked)
            {
				//progressBar1.Visible = false;
				//lbProgress.Visible = false;
			}
			else
            {
				//progressBar1.Visible = true;
				//lbProgress.Visible = true;

			}
        }
    }
}
