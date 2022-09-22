﻿/*
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
						string S2 = S;
						while (S2.IndexOf(':') > 0)
						{
							S2 = S2.Substring(S2.IndexOf(':') + 1);
						}
						oi.references.Add(new RefInfo(LinesCount+1, S2.Substring(0, S2.IndexOf('"')), false));
					}

					if (S.Contains(":startObiekt>"))
    				{
    					oi.StartOb = Convert.ToDateTime(GetXMLValue(S));
    				}
    				if (S.Contains("<bt:poczatekWersjiObiektu>")) 
    				{
    					oi.StartWersjiOb = Convert.ToDateTime(GetXMLValue(S));
    				}
    				if (S.Contains("<bt:koniecWersjiObiektu>")) 
    				{
    					oi.KoniecWersjiOb = Convert.ToDateTime(GetXMLValue(S));
						if (!oi.Deleted) {
							oi.Archived = true;
                        }
						
    				}
    				if (S.Contains(":koniecObiekt>")) 
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
				                    Date2Str(ObI.StartWersjiOb), ";", Date2Str(ObI.KoniecWersjiOb), ";", Date2Str(ObI.KoniecOb), ";", " " );
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
			progressBar1.Visible = true;
			lbProgress.Visible = true;
			int archCount = 0;
			int delCount = 0;
			for (int i=0; i<Objects.Count-1; i++)
			{
				PBValue(i, progressBar1.Maximum);
				//Log("\n"+i.ToString()+" kwo:"+ Objects[i].KoniecWersjiOb.Year.ToString() + " ko:"+ Objects[i].KoniecOb.Year.ToString());
				// jeśli koniec wersji, ale nie koniec obiektu
				//if (Objects[i].KoniecWersjiOb.Year>1 && Objects[i].KoniecOb.Year==1)
				////if (dataGridView1.Rows[i].Cells[4].Value.ToString() != "-" && dataGridView1.Rows[i].Cells[5].Value.ToString() == "-")
				//{
				//	for (int j=0; j < Objects.Count-1; j++)
				// 	{
				//		// jeśli znaleziono duplikat 'lokalnyId'
				//		if (i != j && Objects[i].lokalnyId == Objects[j].lokalnyId)
				// 		//if (i != j && dataGridView1.Rows[i].Cells[2].Value.ToString() == dataGridView1.Rows[j].Cells[2].Value.ToString() )
				// 		{
				// 			// oznacz jako obiekt archiwalny
				// 			archCount++;
				//			dataGridView1.Rows[i].Cells[6].Style.ForeColor = Color.SaddleBrown;
				//			dataGridView1.Rows[i].Cells[6].Style.BackColor = Color.LightGray;
				//			dataGridView1.Rows[i].Cells[6].Value = "Archiwalny";
				// 			Objects[i].Archived = true;
				// 		}
				// 	}
				//}

				// jeśli koniec obiektu
				if (Objects[i].Deleted)
				//if (dataGridView1.Rows[i].Cells[4].Value.ToString() != "-" && dataGridView1.Rows[i].Cells[5].Value.ToString() != "-")
				{
					// oznacz jako obiekt usunięty
					delCount++;
                    //dataGridView1.Rows[i].Cells[6].Style.ForeColor = Color.DarkRed;
                    //dataGridView1.Rows[i].Cells[6].Style.BackColor = Color.LightCoral;
                    //dataGridView1.Rows[i].Cells[6].Value = "Usunięty";
                }
				// lub jeśli koniec wersji - ob. archiwalny
				else if (Objects[i].Archived && !Objects[i].Deleted)
                {
                    // oznacz jako obiekt archiwalny
                    archCount++;
                    //dataGridView1.Rows[i].Cells[6].Style.ForeColor = Color.SaddleBrown;
                    //dataGridView1.Rows[i].Cells[6].Style.BackColor = Color.LightGray;
                    //dataGridView1.Rows[i].Cells[6].Value = "Archiwalny";
                }
			}
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
			Log("\n\nZapis pliku bez obiektów archiwalnych...");
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
			Collection<LinesBlock> blocks = new Collection<LinesBlock>();
			Collection<string> locIdToDelete = new Collection<string>();
			foreach (ObjectInfo o in Objects) 
			{
				if ((o.Archived) || (obTypes.IndexOf(o.Type)>-1))
				{
					blocks.Add(new LinesBlock(o.LineStart, o.LineEnd));
					if (!o.ObKarto) locIdToDelete.Add(o.lokalnyId);
					o.ToRemove = true;
				}
				if ((o.ObKarto) && (checkBox1.Checked))
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
			foreach (ObjectInfo o in Objects)
			{
				if ((o.ObKarto) && (checkBox1.Checked) && (!o.ToRemove))
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
			Log("\nNowy plik:\n  " + newFileName + ext);

			// zapis	

			progressBar1.Maximum = (int)LinesCount;
			progressBar1.Value = 0;
			progressBar1.Visible = true;
			lbProgress.Visible = true;
			bool lnOk;
			using (var writer = new StreamWriter(path))
			{
				long lnCount = 0;
				foreach (var line in File.ReadLines(textBox1.Text))
				{
					lnOk = true;
					PBValue((int)lnCount, progressBar1.Maximum);
					lnCount++;
					// jeśli bieżąca linia jest na liście
					foreach (LinesBlock lb in blocks) 
					{
						if (lnCount >= lb.lnFrom && lnCount <= lb.lnTo) { lnOk = false; }
					}
				
					if (lnOk) 
					{ 
						writer.WriteLine(line); 
					}
					
				}
			}
			Log("\nGotowe.");
			progressBar1.Visible = false;
			lbProgress.Visible = false;

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
	}
}