using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using DarkUI.Forms;
using System.Globalization;

namespace GML_del
{
	/// <summary>
	/// Description of MainForm.
	/// </summary>
	public partial class MainForm : DarkForm
	{
		int LinesCount = 0; 	// ilość linii w pliku GML
		int ObCount = 0;		// liczba obiektów w pliku
		Collection<ObjectInfo> Objects;
		Collection<ObjectType> ObjTypes;
		HashSet<string> LokalneId;			// wszystkie lokalneId
		DateTime startTime;
		float RotAngle = 0;

		DataGridViewCellStyle style1, style2, style3;
		
		public MainForm()
		{
			InitializeComponent();
			this.AllowDrop = true;
			this.DragEnter += new DragEventHandler(Form1_DragEnter);
			this.DragDrop += new DragEventHandler(Form1_DragDrop);
			Objects = new Collection<ObjectInfo>();
			ObjTypes = new Collection<ObjectType>();
			LokalneId = new HashSet<string>();			// lista lokalnychId do szybkiego przeszukiwania
			// ---			
			lbInfo.Text = "";
			statusLabel.Text = "Wybierz plik GML do przetworzenia.";

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
			EndJob();
			if (openFileDialog1.ShowDialog() == DialogResult.OK) 
			{
				btnUruchom.Enabled = false;
				OpenFile(openFileDialog1.FileName);
				
			}
		}
		
		void OpenFile(string fName)
        {
			statusLabel.Text = "Otwieram plik: " + fName;
			Application.DoEvents();
			if (richTextBox1.TextLength > 0)
			{
				Log("\n\n-----\n", Color.WhiteSmoke);
			}
			tbFileName.Text = fName;
			Objects.Clear();
			LokalneId.Clear();
			ObjTypes.Clear();
			dataGridView1.Rows.Clear();
			dataGridView2.Rows.Clear();
			AnalizujPlik(fName);
		}

		void AnalizujPlik(string path) 
		{
			Log("Otwieram Plik: \n   ");
			Log(path, Color.YellowGreen);
			lbInfo.Text = "Analizuję plik. Proszę czekać...";
			LinesCount = 0;
			ObCount = 0;
			int ObKartoCount = 0;
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
						ObKartoCount++;
    				}
    				
   					string S = line.Trim();											// TYP obiektu
    				if (GmlMember) 
    				{    					
    					if (S.Contains("<ges:GES_") || S.Contains("<bdz:BDZ_") || 
							S.Contains("<egb:EGB_") || S.Contains("<ot:OT_"))
						{
    						GmlMember = false;
    						int i = S.IndexOf(':');
    						int j = S.IndexOf(' ');
    						oi.Type = S.Substring(i+1, j-i-1);
							AddObjType(oi.Type);
    					}
    				}

					if (S.Contains(":zrodlo"))
					{
						var V = GetXMLValue(S);
						switch (V)
						{
							case ("pomiarNaOsnowe"):
								oi.Source = 'O';
								break;
							case ("digitalizacjaIWektoryzacja"):
								oi.Source = 'D';
								break;
							case ("fotogrametria"):
								oi.Source = 'F';
								break;
							case ("pomiarWOparciuOElementyMapy"):
								oi.Source = 'M';
								break;
							case ("inne"):
								oi.Source = 'I';
								break;
							case ("nieokreslone"):
								oi.Source = 'X';
								break;
							default:
								break;
						}
					}


					if (S.Contains(":lokalnyId>"))									// LokalnyId
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

					if (S.Contains(":katObrotu>"))								// Kąt obrotu
                    {
						oi.Angle = GetXMLValue(S);
						oi.AngleLine = LinesCount;
					}

					if (S.Contains(":dataPomiaru>"))
					{
						oi.DataPomiaru = Convert.ToDateTime(GetXMLValue(S));
					}

					if (S.Contains("xlink:href"))									// Referencja
					{
						string S2 = GetXlinkID(S);
						oi.references.Add(new RefInfo(LinesCount+1, S2, false));
					}

					if (S.Contains(":startObiekt>"))								// Cykl życia obiektu
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
					if (S.Contains(":rzedna>"))
                    {
						XmlValue.GetValue(S);
						if (!XmlValue.template && !XmlValue.error)
						  oi.H1 = Convert.ToSingle(Sep(XmlValue.value));
                    }
					if (S.Contains(":rzednaGory"))
					{
						XmlValue.GetValue(S);
						if (!XmlValue.template && !XmlValue.error)
							oi.H1 = Convert.ToSingle(Sep(XmlValue.value));
					}
					if (S.Contains(":rzednaDolu"))
					{
						XmlValue.GetValue(S);
						if (!XmlValue.template && !XmlValue.error)
							oi.H2 = Convert.ToSingle(Sep(XmlValue.value));
					}
					if (S.Contains(":srednica"))
					{
						XmlValue.GetValue(S);
						if (!XmlValue.template && !XmlValue.error)
							oi.D = Convert.ToSingle(Sep(XmlValue.value));
					}

				}
			}	
			Log("Ok.\nZnaleziono " + ObCount.ToString() + " ob.");
			Log("\n   w tym " + ObKartoCount.ToString() + " ob. karto");
			Log("\n\nWczytuję do tabeli... ");
			FileInfo fi = new FileInfo(path);
			string fSize = (Math.Round((double)fi.Length / 1024 / 1024, 3)).ToString() + " MB";

			lbInfo.Text = fSize + ",   " + Convert.ToString(LinesCount) + " linii,   " +
				Convert.ToString(ObCount) + " obiektów";
			statusLabel.Text = "Wypełnianie tabeli... ";
			Application.DoEvents();
			
			dataGridView1.SuspendLayout();			
			string txt;
			foreach (ObjectInfo ObI in Objects) 
			{			
				// if (ObI.ObKarto) { continue; }
				txt = String.Concat(ObI.LineStart.ToString(), "-", ObI.LineEnd.ToString() , ";", ObI.Type, ";", ObI.lokalnyId, ";",
									Date2Str(ObI.StartOb), ";", Date2Str(ObI.StartWersjiOb), ";", Date2Str(ObI.KoniecWersjiOb), ";", 
									Date2Str(ObI.KoniecOb), ";", " ", ";", " ", ";", ObI.Angle);
				dataGridView1.Rows.Add(txt.Split(';'));
			}
			dataGridView1.ResumeLayout();
		
			statusLabel.Text = String.Concat("Znaleziono ", LinesCount.ToString(), " linii, ", ObCount.ToString(), " obiektów. Gotowe.");
			Log("Gotowe.\n");

			// Typy obiektów (druga zakładka)
			foreach (ObjectType ot in ObjTypes)
			{
				string S = String.Concat("false;", ot.Type, ";", ot.Count.ToString());
				dataGridView2.Rows.Add(S.Split(';'));
			}

			// Szukanie obiektów archiwalnych i walidacja GMLa
			ValidateGML();
		}
	

		string GetXMLValue(string txt) 
		{
			
			txt = txt.Trim();
			if (txt.Substring(txt.Length-2, 2) == "/>")						// wartość specjalna
			{ 
				if (txt.Contains("xsi:nil"))
                {

                }
			}
			try
            {
				int i = txt.IndexOf('>');
				return txt.Substring(i + 1, txt.Length - (2 * i + 3));
			}
			catch (Exception e)
            {
                DarkMessageBox.ShowWarning(e.Message + "\n\nLinia nr: " + LinesCount.ToString() + "\n\n" + txt, 
					"Błąd wartości XML", DarkDialogButton.Ok);

            }
            return "";
		}

		string SetXMLValue(string lineTxt, string Value)
		{
			try
			{				
				int pos1 = lineTxt.IndexOf('>');						// sprawdzić czy działa !!!
				var txt1 = lineTxt.Substring(0, pos1);
				var txt2 = lineTxt.Substring(pos1 + 1, lineTxt.Length);
				int pos2 = txt2.IndexOf('<');
				txt2 = txt2.Substring(pos2 + 1, txt2.Length);
				txt2 = String.Concat(txt1, Value, txt2);
				return txt2;  //wartość zmieniona
			}
			catch (Exception e)
			{
				DarkMessageBox.ShowWarning(e.Message, "Błąd podmiany wartości XML", DarkDialogButton.Ok);
			}
			return lineTxt;  //wartość niezmieniona
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

		void ValidateGML()																	// ==========================
		{																					//   WALIDACJA DANYCH w GML
			Log("\nSprawdzam status obiektów... ");
			progressBar1.Maximum = Objects.Count;
			progressBar1.Value = 0;
			if (!chBoxSilentMode.Checked) 	{ StartJob(); }
			int archCount = 0;
			int delCount = 0;
			dataGridView1.SuspendLayout();              // <---
			style1 = new DataGridViewCellStyle(this.dataGridView1.RowsDefaultCellStyle);
			style1.ForeColor = Color.DarkRed;
			style1.BackColor = Color.LightCoral;
			style2 = new DataGridViewCellStyle(this.dataGridView1.RowsDefaultCellStyle);
			style2.ForeColor = Color.SaddleBrown;
			style2.BackColor = Color.LightGray;
			style3 = new DataGridViewCellStyle(this.dataGridView1.RowsDefaultCellStyle);
			style3.ForeColor = Color.Black;
			style3.BackColor = Color.Coral;
			string txtErrors = "";
			int errCount = 0;
			for (int i = 0; i < Objects.Count - 1; i++)
			{
				if (!chBoxSilentMode.Checked)
				PBValue(i, progressBar1.Maximum);
				txtErrors = "";

				// jeśli koniec obiektu
				if (Objects[i].Deleted)
				{
					// oznacz jako obiekt usunięty
					delCount++;
					if (!chBoxSilentMode.Checked)
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
					if (!chBoxSilentMode.Checked)
					{
						dataGridView1.Rows[i].Cells["StatusOb"].Style = style2;
						dataGridView1.Rows[i].Cells["StatusOb"].Value = "Archiwalny";
					}
				}

				// kontrola rzędnych
				if (Objects[i].H1 != -999.0f)
                {
					if (Objects[i].H1 < -100 || Objects[i].H1 > 1000)
					{
						txtErrors = setError(i, txtErrors, "Błąd wysokości (" + Objects[i].H1.ToString() + ")");
					}
                }

				if (Objects[i].H2 != -999.0f)
				{
					if (Objects[i].H2 < -100 || Objects[i].H2 > 1000)
					{
						txtErrors = setError(i, txtErrors, "Błąd wysokości (" + Objects[i].H2.ToString() + ")");
					}
				}

				//kontrola średnicy
				if (Objects[i].D != -999.0f)
				{
					if (Objects[i].D <= 0)
					{
						txtErrors = setError(i, txtErrors, "Błędna średnica ("+ Objects[i].D.ToString()+")");
					}
				}


				// Wpisanie UWAG jeśli są
				if (txtErrors.Length > 0) 
				{
					errCount++;
					dataGridView1.Rows[i].Cells["Uwagi"].Value = txtErrors;
					dataGridView1.Rows[i].Cells["Uwagi"].ToolTipText = txtErrors;
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
			if (errCount>0)
            {
				Log("\nLiczba obiektów z błędami: " + errCount.ToString(), Color.Coral);
			}
			EndJob();
			btnUruchom.Enabled = true;
		}


		// Zamknięcie programu
		void Button2Click(object sender, EventArgs e)
		{
			this.Close();
		}
		
		
		public class LinesBlock										// Blok linii określających obiekt w GML
		{
			public long lnFrom;
			public long lnTo;
			
			public LinesBlock(long lFrom, long lTo)
			{
				lnFrom = lFrom;
				lnTo = lTo;
			}
		}
		
		void btnUruchomClick(object sender, EventArgs e)						// ZAPIS 
		{																		// =======================================
			RotAngle = Convert.ToSingle(tbAngle.Text);
			// usunięcie ob. archiwalnych
			// lista linii od których pominięcie
			Log("\n-\nZapis obiektów do nowego pliku.");
			// lista typów obiektów do usunięcia
			HashSet<string> obTypes = new HashSet<string>();				
			for (int i=0; i<ObjTypes.Count; i++)
            {
				if (Convert.ToBoolean(dataGridView2.Rows[i].Cells[0].Value))
                {
					obTypes.Add(ObjTypes[i].Type);
                }
            }
			// bloki linii w pliku do pominięcia
			Log("\n- Oznaczanie linii do pominięcia...", Color.LimeGreen);
			Collection<LinesBlock> blocks = new Collection<LinesBlock>();
			// lista lokalnychId do usunięcia
			HashSet<string> locIdToDelete = new HashSet<string>();			
			progressBar1.Maximum = ObCount;
			progressBar1.Value = 0;
			StartJob();
			int obc = 0;
			int obKarto2Del = 0;
			int ob2Del = 0;
			int ang2Rotate = 0;
			foreach (ObjectInfo o in Objects)												// ZAPIS - przejście 1
			{
				obc++;
				PBValue(obc, ObCount);
				if (chBoxOneJob.Checked)   // pojedyncze akcje z comboBoxa
                {
					switch (cbOneJob.SelectedIndex)
                    {
						case 0:                                 // tylko ob. usunięte
							if (!o.Deleted)
							{
								o.ToRemove = true;
								blocks.Add(new LinesBlock(o.LineStart, o.LineEnd));
								ob2Del++;
							}
							break;
						case 1:
							// jeśli to obiekt archiwalny lub wersja obiektu sprzed daty pomiaru to usuwamy
							if (o.Archived || o.StartWersjiOb < dateTimePicker1.Value) 
                            {
								o.ToRemove = true;
								blocks.Add(new LinesBlock(o.LineStart, o.LineEnd));
								ob2Del++;
							}
							break;
						default:
							break;
                    }
					continue;
                }
				 
				// jeśli to ob. archiwalny lub typ obiektu do usunięcia
				if ((o.Archived && chBoxArch.Checked) || (obTypes.Contains(o.Type)))
				{
					blocks.Add(new LinesBlock(o.LineStart, o.LineEnd));
					//jeśli to nie ob.karto, to dodaj lokalnyId do listy do usunięcia
					if (!o.ObKarto) locIdToDelete.Add(o.lokalnyId);
					o.ToRemove = true;
					ob2Del++;
				}

				// Ob. karto z relacją do nieistniejących obiektów
				if ((o.ObKarto) && (chBoxKarto.Checked) && (o.references.Count > 0))
                {
					//Log("\nKarto: " + o.lokalnyId, Color.LightSteelBlue);
					if (!LokalneId.Contains(o.references[0].lokalnyId))
					{
						//Log("\nKarto: "+o.references[0].lokalnyId, Color.LightSteelBlue);
						blocks.Add(new LinesBlock(o.LineStart, o.LineEnd));
						o.ToRemove = true;
						obKarto2Del++;
						ob2Del++;
					}
				}
			}
			if (obKarto2Del > 0) 
			    Log("\n   Znaleziono " + obKarto2Del.ToString() + " ob. karto z relacją\n      do nieistniejących obiektów.", Color.LightCoral);

			// usuwanie obiektów karto z referencją do usuwanych obiektów
			// oraz pomijanych typów obiektów
			Log("\n- Oznaczanie ob. karto z relacją do usuwanych obiektów...", Color.LimeGreen);
			progressBar1.Maximum = ObCount;
			progressBar1.Value = 0;
			StartJob();
			obc = 0;
			obKarto2Del = 0;
			int obRotate = 0;
			foreach (ObjectInfo o in Objects)                                                   // ZAPIS - przejście 2
			{
				obc++;
				if ((obc / 100) - Math.Truncate((double)(obc / 100)) == 0)
				{
					// co setny obiekt...
					PBValue(obc, ObCount);
				}
				if (!o.ToRemove)
				{
					// jeśli ob. karto ma referencję do usuwanego obiektu
					if ((o.ObKarto) && (o.references.Count > 0))
					{
						
						if (locIdToDelete.Contains(o.references[0].lokalnyId))
						{
							blocks.Add(new LinesBlock(o.LineStart, o.LineEnd));
							o.ToRemove = true;
							obKarto2Del++;
							ob2Del++;
						}
					}
					// jeśli obracamy kąt, a nie jest to obiekt usuwany lub archiwalny
					if ((chBoxRotateNew.Checked) && (!o.Deleted) && (!o.Archived) && (o.Angle != "brak"))
					{
						// jeśli obiekt jest nowy
						if (o.StartOb >= dateTimePicker1.Value)
                        {
							o.RotateAngle = true;
							o.Angle = RotateAngle(o.Angle);
							obRotate++;
                        }
						// lub jest to nowa wersja
						else if ((chBoxRotateMod.Checked) && (o.StartWersjiOb >= dateTimePicker1.Value))
						{
							o.RotateAngle = true;
							o.Angle = RotateAngle(o.Angle);
							obRotate++;
						}
					}
				}
			}
			if (obKarto2Del > 0)
				Log("\n   Znaleziono " + obKarto2Del.ToString() + " ob. karto z relacją do usuwanych obiektów.", Color.LightCoral);
			if (obRotate > 0)
				Log("\n   Znaleziono " + obRotate.ToString() + " kątów do obrócenia. (NIE DZIAŁA)", Color.Gray);

			// Sortowanie listy bloków do usunięcia
			Log("\nPorządkowanie listy linii do usunięcia... ");
			if (blocks.Count>1)
				blocks = QuickSortBlocks(blocks, 0, blocks.Count - 1);
			Log("OK\n");
			// nazwa nowego pliku
			string path = tbFileName.Text;
			string newFileName = Path.GetFileNameWithoutExtension(tbFileName.Text) + tbKonc.Text;
			string dir = Path.GetDirectoryName(path);
			string ext = Path.GetExtension(path);
			path =  Path.Combine(dir, newFileName + ext);
			Log("\nTworzenie nowego pliku:\n  ");
			Log(newFileName + ext, Color.YellowGreen);

			// zapis	
			Log("\n\nZapisywanie...");
			progressBar1.Maximum = (int)LinesCount;
			progressBar1.Value = 0;
			statusLabel.Text = "Zapisywanie...  (" + ob2Del.ToString() + " ob. do pominięcia)";
			StartJob(); ;
			int linesBlockIdx = 0; 
			bool lnOk;

			using (var writer = new StreamWriter(path))                                         // ZAPIS - właściwy
			{
				long lnCount = 0;
				foreach (var line in File.ReadLines(tbFileName.Text))
				{
					lnOk = true;

					PBValue((int)lnCount, progressBar1.Maximum);

					if (linesBlockIdx < blocks.Count - 1)
                    {
                        if (lnCount < blocks[linesBlockIdx].lnFrom)
                        { lnOk = true; }
                        else if (lnCount >= blocks[linesBlockIdx].lnFrom && lnCount < blocks[linesBlockIdx].lnTo)
                        { lnOk = false; }
                        else if (lnCount == blocks[linesBlockIdx].lnTo)
                        {
                            lnOk = false;
                            linesBlockIdx++;
                            statusLabel.Text = "Zapisywanie...  (Pominięto " + linesBlockIdx.ToString() +
                              " z " + ob2Del.ToString() + " ob.)";
                        }
						else { lnOk = true; }
					}
					lnCount++;

					// obrót kąta
					//if (line.Contains("<bt:katObrotu>"))
                    //{
					//	line.Replace
                    //}

					// jeśli linia ok
					if (lnOk) 
					{ 
						writer.WriteLine(line); 
					}
				}
			}
			Log("\nGotowe.");
			statusLabel.Text = "Zapis z pominięciem " + ob2Del.ToString() + " obiektów zakończony.";

			EndJob(); ;
		}


		public Collection<LinesBlock> QuickSortBlocks(Collection<LinesBlock> coll, int leftIndex, int rightIndex)
		{
			var i = leftIndex;
			var j = rightIndex;
			var pivot = coll[leftIndex].lnFrom;
			while (i <= j)
			{
				while (coll[i].lnFrom < pivot)
				{
					i++;
				}

				while (coll[j].lnFrom > pivot)
				{
					j--;
				}
				if (i <= j)
				{
					LinesBlock temp = coll[i];
					coll[i] = coll[j];
					coll[j] = temp;
					i++;
					j--;
				}
			}

			if (leftIndex < j)
				QuickSortBlocks(coll, leftIndex, j);
			if (i < rightIndex)
				QuickSortBlocks(coll, i, rightIndex);
			return coll;
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
			new AboutForm().ShowDialog();
		}

		private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {
			cbOneJob.Enabled = chBoxOneJob.Checked;
			chBoxKarto.Enabled = !chBoxOneJob.Checked;
			chBoxArch.Enabled = !chBoxOneJob.Checked;
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
			if (chBoxSilentMode.Checked)
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

        private void timer1_Tick(object sender, EventArgs e)
        {
			var elapsedTime = (DateTime.Now - startTime).TotalSeconds;
			var totalTime = elapsedTime * progressBar1.Maximum / progressBar1.Value;
			TimeSpan time = TimeSpan.FromSeconds(elapsedTime);
			lbTime.Text = TimeSpan.FromSeconds(elapsedTime).ToString(@"hh\:mm\:ss");
			lbTime2.Text = TimeSpan.FromSeconds(totalTime-elapsedTime).ToString(@"hh\:mm\:ss");
		}

        private void StartJob()
        {
			startTime = DateTime.Now;
			timer1.Enabled = true;
			panel1.Visible = true;
        }

		private void EndJob()
		{
			timer1.Enabled = false;
			panel1.Visible = false;
		}


		private string RotateAngle(string angle)
        {
			float a = Convert.ToSingle(angle);
			a = a + RotAngle;
			while (a < 0) { a = a + 400; }
			while (a > 400) { a = a - 400; }
			return a.ToString();
		}

        private void dateTimePicker1_ValueChanged(object sender, EventArgs e)
        {
			dateTimePicker1.Value = dateTimePicker1.Value.Date;
        }


		private string Sep(string num)
		{
			string sep = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
			if (sep == ".")
			{
				return num.Replace(',', '.');
			}
			else
			{
				return num.Replace('.', ',');
			}
		}

        private void toolStripButton1_Click(object sender, EventArgs e)
        {
			dataGridView1.FirstDisplayedScrollingRowIndex = 0;
        }


		private void toolStripButton2_Click(object sender, EventArgs e)
        {
			int selIndex = dataGridView1.CurrentCell.RowIndex;
			for (int i=selIndex+1; i<dataGridView1.Rows.Count; i++)
            {
				if (!(dataGridView1.Rows[i].Cells["Uwagi"].Value is null))
                {
					if (dataGridView1.Rows[i].Cells["Uwagi"].Value.ToString().Length > 0)
                    {
						//Log("\n" + dataGridView1.Rows[i].Cells["Uwagi"].Value.ToString());
						//dataGridView1.Rows[i].Selected = true;
						dataGridView1.FirstDisplayedScrollingRowIndex = i;
						Application.DoEvents();
						break;
					}
				}
            }
			statusLabel.Text = "Nie znaleziono więcej linii z błedami.";
			Application.DoEvents();
		}

		private void toolStripButton3_Click(object sender, EventArgs e)
		{
			int selIndex = dataGridView1.CurrentCell.RowIndex;
			for (int i = selIndex-1; i > 0; i--)
			{
				if (!(dataGridView1.Rows[i].Cells["Uwagi"].Value is null))
				{
					if (dataGridView1.Rows[i].Cells["Uwagi"].Value.ToString().Length > 0)
					{
						//Log("\n" + dataGridView1.Rows[i].Cells["Uwagi"].Value.ToString());
						//dataGridView1.Rows[i].Selected = true;
						dataGridView1.FirstDisplayedScrollingRowIndex = i;
						Application.DoEvents();
						break;
					}
				}
			}
			statusLabel.Text = "Nie znaleziono więcej linii z błedami.";
			Application.DoEvents();
		}



		private string setError(int rowIdx, string oldErrors, string newError)
        {
			dataGridView1.Rows[rowIdx].Cells[0].Style = style3;
			dataGridView1.Rows[rowIdx].Cells[1].Style = style3;
			dataGridView1.Rows[rowIdx].Cells[2].Style = style3;
			dataGridView1.Rows[rowIdx].Cells["Uwagi"].Style = style3;
			if (oldErrors.Length > 0)
				{ oldErrors = String.Concat(oldErrors, "\n", newError); }
			else
				{ oldErrors = String.Concat(oldErrors, newError); }
			return oldErrors;
		}

	}
}
