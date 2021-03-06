using System;
using System.Collections.Generic;
using System.IO;
using Quanty.Properties;
using Mascot;

namespace Quanty {
    class QMSMSList{
        class QMSMSEntry{
            public bool Approved;
            public string Comment;
            public MascotSpectra ms;
            public QMatch Match;
            public int FSN;
        }

        List<QMSMSEntry> Entries;

        QMSMSEntrybyFSN byFSN;

        public QMSMSList(MascotParser mp){
            Entries = new List<QMSMSEntry>();
            QMSMSEntry Entry;
            for ( int i=0 ; i < mp.Spectra.Count ; i++){
                Entry = new QMSMSEntry();
                Entry.Approved = true;
                Entry.ms = mp.Spectra[i];
                Entry.Match = null;
                Entry.FSN = mp.Spectra[i].ScanNumber;
                Entries.Add(Entry);
            }
            byFSN = new QMSMSEntrybyFSN();
            Entries.Sort(byFSN);
        }

        class QMSMSEntrybyFSN : IComparer<QMSMSEntry> {
            public int Compare(QMSMSEntry x, QMSMSEntry y){
                if (x.FSN>y.FSN) { return 1;} 
                else if (x.FSN == y.FSN ) { return 0; }
                else return -1;
            }
        }
        
        public void SetReason(int FSN, string Comment, bool App){
            QMSMSEntry SearchEntry = new QMSMSEntry();
            SearchEntry.FSN = FSN;
            int index = Entries.BinarySearch(SearchEntry,byFSN);
            Entries[index].Comment = Comment;
            Entries[index].Approved = App;
        }

        public void Out(StreamWriter sw){
            for ( int i = 0 ; i < Entries.Count ; i++){
                sw.WriteLine("{0}\t{1}\t{2}",Entries[i].ms.ScanNumber,Entries[i].ms.mz,Entries[i].Comment);
            }
        }
    }

    public class SpectrabySeq: IComparer<Mascot.MascotSpectra> {
        public int Compare(Mascot.MascotSpectra x, Mascot.MascotSpectra y){
            if (x.Peptides.Count == 0 && y.Peptides.Count == 0 ) {return 0;}
            if (x.Peptides.Count == 0 ) {return -1;}
            if (y.Peptides.Count == 0 ) {return 1;}
            int cp=x.Peptides[0].Sequence.CompareTo(y.Peptides[0].Sequence);
            if (cp!=0) return cp;
            for ( int k = 0 ; k < x.Peptides[0].ModIndex.GetLength(0) ; k++){
                if (x.Peptides[0].ModIndex[k]>y.Peptides[0].ModIndex[k]) {return 1;}
                if (x.Peptides[0].ModIndex[k]<y.Peptides[0].ModIndex[k]) {return -1;}
            }
            if (x.Peptides[0].NModIndex>y.Peptides[0].NModIndex) {return 1;}
            if (x.Peptides[0].NModIndex<y.Peptides[0].NModIndex) {return -1;}
            if (x.Peptides[0].CModIndex>y.Peptides[0].CModIndex) {return 1;}
            if (x.Peptides[0].CModIndex<y.Peptides[0].CModIndex) {return -1;}
            return 0;
        }
    }
    class MascotProc{

        public static ILogAndProgress iLog = null;

        //����� ��������� �������� ��� �������� ����� ������ ������� 
        //string ������������ ��� value type, �� ����� ��������� ���������� ���������� IEquatable ��� reference type 
        static bool StringContains(ref List<string> L, string str){
            for (int i = 0 ; i < L.Count ; i++ ){
                if (L[i] == str) {
                    return true; 
                }
            }
            return false;
        }

        static void StringRemove(ref List<string> L, string str){
            for (int i = 0 ; i < L.Count ; i++ ){
                if (L[i] == str) {
                    L.Remove(L[i]);
                    return; 
                }
            }
        }

        static public void FillMSMSLists(
            ref List<QPeptide> Mascots, 
            ref List<QPeptide> MSMSList, 
            ref List<QProtein> Proteins,
            MascotParser mp ){

            Mascots = new List<QPeptide>();
            MSMSList = new List<QPeptide>();
            QPeptide data;
            int Best,i,j,k;
            SpectrabySeq comp = new SpectrabySeq();
            mp.Spectra.Sort(comp);

            for (i = 0 ; i < mp.Spectra.Count ; i++){
                //���� ��� �������� 
                if (mp.Spectra[i].Peptides.Count == 0) {
                    data = new QPeptide(mp,i);
                    data.Case = "No peptides identified";
                    MSMSList.Add(data);
                    continue;
                }
                //���� � �������� ������� ��������� Score
                if (mp.Spectra[i].Peptides[0].Score < Convert.ToDouble(Settings.Default.MascotScore)){
                    data = new QPeptide(mp,i);
                    data.Case = "Peptides identified under score limit";
                    MSMSList.Add(data);
                    continue;
                }

                Best = i;
                if (Settings.Default.MascotScoreFiltering){
                    for (j = i+1; j< mp.Spectra.Count ; j++){
                        if (mp.Spectra[i].Peptides[0].Sequence != mp.Spectra[j].Peptides[0].Sequence){
                            break;
                        }
                        //���������� ������������ ������ �����������
                        for(k = 0 ; k< mp.Spectra[i].Peptides[0].ModIndex.GetLength(0) ; k++){
                            if (mp.Spectra[i].Peptides[0].ModIndex[k] !=  
                                mp.Spectra[j].Peptides[0].ModIndex[k] ){
                                break;
                            }
                        }
                        if (k<mp.Spectra[i].Peptides[0].ModIndex.GetLength(0) || 
                            mp.Spectra[i].Peptides[0].CModIndex != mp.Spectra[j].Peptides[0].CModIndex || 
                            mp.Spectra[i].Peptides[0].NModIndex != mp.Spectra[j].Peptides[0].NModIndex  ) break;
                        //����� ������� �� Score
                        if (mp.Spectra[j].Peptides[0].Score > mp.Spectra[Best].Peptides[0].Score){
                            Best = j;
                        }
                    }
                    //�������� �������� � ������ ���� MS/MS
                    for (k = i ; k < j ; k++ ){
                        if (k != Best){
                            data = new QPeptide(mp,k);
                            data.Case = string.Format("Not the best score ({0}) for peptide {1} (Max={2})",
                                    mp.Spectra[k].Peptides[0].Score,
                                    mp.Spectra[k].Peptides[0].Sequence,
                                    mp.Spectra[Best].Peptides[0].Score);
                            MSMSList.Add(data);
                        }
                    }
                    i = j-1; 
                }

                //������ ��� ������� �� Score ���� ������ ��������������� ����� ������ ���� 
                data = new QPeptide(mp,Best);
                Mascots.Add(data);
            }
            
            //��������� IPI
            //������ �� ����� ��������������� ���������� IPI �� ����������� ������ ����� 
            //����� �������� ���������������� ���� IPI �������� ������������� (�������� ������)
            //�������� ���������������� ������ IPI

            //�������� ����� ��� �� ��� ������� �������� �������������� ������ 
            List<string> Processed = new List<string>(); //������ � ������� ������������� 
            List<string> ipiset = new List<string>(); //������ ���� IPI ������� ����� ������������ 
            List<string> NotDeps = new List<string>();
            string[] dataexipis = null;
            foreach ( QPeptide dataex in Mascots){ //��� ������� ������� 
                dataexipis = new string[dataex.IPIs.Count]; 
                dataex.IPIs.CopyTo(dataexipis);
                foreach (string ipi in dataexipis){
                    if (!StringContains(ref Processed,ipi)){ //���� ������ ipi ��� �� ��������� � ������ ������������
                        ipiset.Clear();                     //���������� ��������� ���� IPI ������� ����� ������������ 
                        ipiset.AddRange(dataex.IPIs);
                        StringRemove(ref ipiset,ipi);
                        foreach(QPeptide chdata in Mascots){ //������������ ������ ����� ���������� � ������ ������� �������� 
                            if (dataex.MascotRT != chdata.MascotRT &&   
                                StringContains(ref chdata.IPIs,ipi)){
                                //� ���� ������ �� ���������� ��� dataex � chdata ����� ����� ipi
                                foreach(string ipicheck in ipiset){
                                    //����� �� �������� � Nodepts �������������� 
                                    //������� �� ���������� ������ � ipi 
                                    //����� ������ �������������� � NoDepts �������� ��� ���������� ���� �� ���� ������ 
                                    // ����������� � ipi �� �� ����������� � ����� �������������� 
                                    //� ������������� ipi �� �������� ��������� �� ������ �������������� 
                                    if(!StringContains(ref chdata.IPIs,ipicheck) && 
                                       !StringContains(ref NotDeps,ipicheck)){  //���� ���� ���� �� ���� ������ � ����������� IPI 
                                        NotDeps.Add(ipicheck);                  //�� ������� �����-�� �� IPI ���������� ������
                                                                                //�� ������� IPI �� �������� ��������� �� ��������������
                                    }
                                }
                            }
                        }
                        //���� ���� ���� �� ���� ������������� ������� ������ ����������� ������ � ipi 
                        //�� � ������ Nodepts �� ������� �� ������� � ��������� ������ Nodepts ����� ��������� 
                        //���� ��� ipi ������� �� ������� �������������� � ������ ���� �������� �� ������ 
                        //��������������� �� ���� ��������
                        if (ipiset.Count != NotDeps.Count) { 
                            foreach(QPeptide chdata in Mascots){
                                StringRemove(ref chdata.IPIs,ipi);
                            }
                        }else{
                            Processed.Add(ipi);
                        }
                        NotDeps.Clear();
                    }
                }
            }

            iLog.RepProgress(50);
            //� ������ ��������� ������ ����������������
            List<QPeptide> OldMascots = Mascots;
            Mascots = new List<QPeptide>();

            for (i = 0 ; i < OldMascots.Count ; i++ ){
                if ( OldMascots[i].IPIs.Count == 1){
                    QPeptide remdata = OldMascots[i];
                    remdata.IPI = remdata.IPIs[0];
                    Mascots.Add(remdata);
                }else{
                    string Mess = string.Format("This entry ambigously defined as part of {0} Proteins: ",OldMascots[i].IPIs.Count);
                    for (j = 0 ; j < OldMascots[i].IPIs.Count ; j++){
                        Mess = Mess + OldMascots[i].IPIs[j]+ ", ";
                    }
                    Mess = Mess.Substring(0,Mess.Length-2);
                    OldMascots[i].Case = Mess;
                    MSMSList.Add(OldMascots[i]);
                }
            }
            iLog.RepProgress(60);
            //������ ��������� �� ������ 
            Mascots.Sort(new QPeptide.byIPIs());

            //������ ��������� �� ��������� ������������ ���������� �������� 
            iLog.RepProgress(70);

            QProtein Prot;
            Proteins = new List<QProtein>();

            int Count = 0;
            string preIPI = "";
            //!! ����� ��� ��������� ����������� ����� 
            for (i = 0 ; i <= Mascots.Count ; i++){
                if (i < Mascots.Count && Mascots[i].IPI == preIPI){
                    Count++;
                }else{
                    for (j = i-1 ; i-j<=Count ; j--){
                        QPeptide temp = Mascots[j];
                        temp.peptides = Count;
                        Mascots[j] = temp;
                    }
                    if ( Count >= Convert.ToInt32(Settings.Default.PeptperProt)){
                        Prot = new QProtein();
                        Prot.Peptides = new List<QPeptide>();
                        Prot.ipis = Mascots[i-1].IPIs;
                        Prot.ipi = Mascots[i-1].IPI;
                        Prot.Est = 0.0;
                        for (j = i-1 ; i-j<=Count ; j--){
                            Prot.Peptides.Add(Mascots[j]);
                        }
                        //�������� �� ����� ������ �������� � ����� ����� 

                        for ( j = 0 ; j < mp.Proteins.Count ; j++){
                            if (mp.Proteins[j].Name == "\""+Mascots[i-1].IPI+"\""){
                                break;
                            }
                        }
                        if (j < mp.Proteins.Count){
                            Prot.Name = mp.Proteins[j].Name;
                            Prot.Desc = mp.Proteins[j].Descr;
                        }else{
                            Prot.Name = Prot.ipi;
                            Prot.Desc = "This protein is not described in source .dat file. See initial FASTA file for protein description";
                        }
                        Proteins.Add(Prot);
                    }else{
                        for (j = i-1 ; i-j<=Count ; j--){
                            Mascots[j].Case = string.Format("This entry belongs to \"{0}\" protein which have only {1} uniquely identified peptide(s)",Mascots[j].IPI,Count);
                            MSMSList.Add(Mascots[j]);
                        }
                    }
                    Count = 1;
                    if (i < Mascots.Count){
                        preIPI = Mascots[i].IPI;
                    }
                }
            }

            iLog.RepProgress(80);

            OldMascots = Mascots;
            Mascots = new List<QPeptide>();

            for (i = 0 ; i < OldMascots.Count ; i++ ){
                if ( OldMascots[i].peptides >= Convert.ToInt32(Settings.Default.PeptperProt)){
                    QPeptide remdata = OldMascots[i];
                    remdata.Case = string.Format("This entry belongs to \"{0}\" quntified protein",remdata.IPI);
                    Mascots.Add(remdata);
                    MSMSList.Add(remdata);
                }else{
                    continue;
                }
            }
            iLog.RepProgress(90);

            //������ ��������� �� ������ � ������ ��� 
            Mascots.Sort( new QPeptide.byMascotSN());
            //� ��������� ������������� ��������� ������ �������� 

            iLog.RepProgress(100);

        }
    }


}
