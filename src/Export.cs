using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Security.AccessControl;
using System.Security.Principal;
namespace Astra {
static class SecureFiles {
 public static FileStream Open(string path,bool append){
  if(append&&!File.Exists(path))throw new IOException("Append file does not exist.");
  if(File.Exists(path)&&(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new IOException("Refusing to write private keys to a reparse point.");
  var acl=new FileSecurity();acl.SetAccessRuleProtection(true,false);var sid=WindowsIdentity.GetCurrent().User;
  acl.AddAccessRule(new FileSystemAccessRule(sid,FileSystemRights.FullControl,AccessControlType.Allow));acl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid,null),FileSystemRights.FullControl,AccessControlType.Allow));
  var stream=new FileStream(path,append?FileMode.Open:FileMode.CreateNew,FileSystemRights.Read|FileSystemRights.Write|FileSystemRights.ReadPermissions|FileSystemRights.ChangePermissions,FileShare.None,65536,FileOptions.SequentialScan,acl);
  try{stream.SetAccessControl(acl);if(append)stream.Seek(0,SeekOrigin.End);return stream;}catch{stream.Dispose();throw;}
 }
 public static void Text(Stream stream,string text){byte[] bytes=Encoding.UTF8.GetBytes(text);try{stream.Write(bytes,0,bytes.Length);}finally{Crypto.Wipe(bytes);}}
 public static void Key(Stream stream,byte[] key){byte[] bytes=new byte[64];const string hex="0123456789abcdef";try{for(int i=0;i<32;i++){bytes[2*i]=(byte)hex[key[i]>>4];bytes[2*i+1]=(byte)hex[key[i]&15];}stream.Write(bytes,0,bytes.Length);}finally{Crypto.Wipe(bytes);}}
}
// Streaming OOXML without Excel/COM or an external spreadsheet runtime.
// Each published part is a complete workbook. Private XML journals survive export errors.
sealed class ExcelExport:IDisposable {
 public const int RowsPerPart=10000;
 const string Ns="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
 readonly string stem,network;readonly int partLimit;FileStream journal;string journalPath;int rows,part;public long Published;public int Parts;
 public ExcelExport(string baseName,string label,int limit=RowsPerPart){stem=baseName;network=Escape(label);partLimit=limit;Begin();}
 static string Escape(string s){return System.Security.SecurityElement.Escape(s);}
 void Begin(){part++;rows=0;journalPath=stem+"_"+part.ToString("D4")+".xlsx.pending";journal=SecureFiles.Open(journalPath,false);}
 public void Add(Wallet w){if(journal==null)Begin();int row=rows+4;
  SecureFiles.Text(journal,"<row r=\""+row+"\" ht=\"23\" customHeight=\"1\">");
  Cell(journal,"A"+row,network,rows%2==0?3:4);Cell(journal,"B"+row,Escape(w.Address),rows%2==0?5:6);
  SecureFiles.Text(journal,"<c r=\"C"+row+"\" t=\"inlineStr\" s=\""+(rows%2==0?6:7)+"\"><is><t>");SecureFiles.Key(journal,w.Key);SecureFiles.Text(journal,"</t></is></c></row>");rows++;
  if(rows>=partLimit)Publish();
 }
 static void Cell(Stream s,string reference,string escaped,int style){SecureFiles.Text(s,"<c r=\""+reference+"\" t=\"inlineStr\" s=\""+(style+1)+"\"><is><t>"+escaped+"</t></is></c>");}
 public void Flush(){if(journal!=null)journal.Flush(true);}
 public void Publish(){if(journal==null||rows==0)return;Flush();string path=stem+"_"+part.ToString("D4")+".xlsx",temporary=path+".writing";
  using(var file=SecureFiles.Open(temporary,false)){
   using(var zip=new ZipArchive(file,ZipArchiveMode.Create,true)){
    Entry(zip,"[Content_Types].xml","<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/></Types>");
    Entry(zip,"_rels/.rels","<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
    Entry(zip,"xl/workbook.xml","<workbook xmlns=\""+Ns+"\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><bookViews><workbookView/></bookViews><sheets><sheet name=\"Results\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
    Entry(zip,"xl/_rels/workbook.xml.rels","<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/><Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/></Relationships>");
    Entry(zip,"xl/styles.xml",Styles);
    using(var sheet=zip.CreateEntry("xl/worksheets/sheet1.xml",CompressionLevel.Fastest).Open()){
     SecureFiles.Text(sheet,"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><worksheet xmlns=\""+Ns+"\"><dimension ref=\"A1:C"+(rows+3)+"\"/><sheetViews><sheetView workbookViewId=\"0\" showGridLines=\"0\"><pane ySplit=\"3\" topLeftCell=\"A4\" activePane=\"bottomLeft\" state=\"frozen\"/><selection pane=\"bottomLeft\" activeCell=\"A4\" sqref=\"A4\"/></sheetView></sheetViews><sheetFormatPr defaultRowHeight=\"23\"/><cols><col min=\"1\" max=\"1\" width=\"38\" customWidth=\"1\"/><col min=\"2\" max=\"2\" width=\"48\" customWidth=\"1\"/><col min=\"3\" max=\"3\" width=\"72\" customWidth=\"1\"/></cols><sheetData><row r=\"1\" ht=\"32\" customHeight=\"1\">");Cell(sheet,"A1","Astra v1.1.0 · Generated addresses",0);SecureFiles.Text(sheet,"</row><row r=\"2\" ht=\"25\" customHeight=\"1\">");Cell(sheet,"A2","PLAINTEXT PRIVATE KEYS: Do not share. Addresses and keys are stored as text to preserve leading zeros.",1);SecureFiles.Text(sheet,"</row><row r=\"3\" ht=\"28\" customHeight=\"1\">");Cell(sheet,"A3","Network",2);Cell(sheet,"B3","Address",2);Cell(sheet,"C3","Private key (64 hex characters)",2);SecureFiles.Text(sheet,"</row>");
     journal.Position=0;byte[] buffer=new byte[65536];try{int read;while((read=journal.Read(buffer,0,buffer.Length))>0)sheet.Write(buffer,0,read);}finally{Crypto.Wipe(buffer);}journal.Position=journal.Length;
     SecureFiles.Text(sheet,"</sheetData><autoFilter ref=\"A3:C"+(rows+3)+"\"/><mergeCells count=\"2\"><mergeCell ref=\"A1:C1\"/><mergeCell ref=\"A2:C2\"/></mergeCells></worksheet>");
    }
   }file.Flush(true);
  }
  File.Move(temporary,path);Published+=rows;Parts++;journal.Dispose();journal=null;File.Delete(journalPath);rows=0;
 }
 public void Finish(){Publish();if(journal!=null){journal.Dispose();journal=null;File.Delete(journalPath);}}
 static void Entry(ZipArchive zip,string path,string xml){using(var s=zip.CreateEntry(path,CompressionLevel.Fastest).Open())SecureFiles.Text(s,"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"+xml);}
 const string Styles="<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><fonts count=\"4\"><font><sz val=\"16\"/><color rgb=\"FFFFFFFF\"/><name val=\"Segoe UI\"/><b/></font><font><sz val=\"10\"/><color rgb=\"FF805A20\"/><name val=\"Segoe UI\"/></font><font><sz val=\"11\"/><color rgb=\"FF20354A\"/><name val=\"Segoe UI\"/></font><font><sz val=\"11\"/><color rgb=\"FF20354A\"/><name val=\"Consolas\"/></font></fonts><fills count=\"5\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill><fill><patternFill patternType=\"solid\"><fgColor rgb=\"FF123E43\"/><bgColor indexed=\"64\"/></patternFill></fill><fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFE9F3F1\"/><bgColor indexed=\"64\"/></patternFill></fill><fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFF4F7FA\"/><bgColor indexed=\"64\"/></patternFill></fill></fills><borders count=\"1\"><border/></borders><cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"2\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs><cellXfs count=\"8\"><xf numFmtId=\"0\" fontId=\"2\" fillId=\"0\" borderId=\"0\"/><xf numFmtId=\"49\" fontId=\"0\" fillId=\"2\" borderId=\"0\" applyAlignment=\"1\"><alignment vertical=\"center\" indent=\"1\"/></xf><xf numFmtId=\"49\" fontId=\"1\" fillId=\"0\" borderId=\"0\"/><xf numFmtId=\"49\" fontId=\"2\" fillId=\"3\" borderId=\"0\"/><xf numFmtId=\"49\" fontId=\"2\" fillId=\"0\" borderId=\"0\"/><xf numFmtId=\"49\" fontId=\"2\" fillId=\"4\" borderId=\"0\"/><xf numFmtId=\"49\" fontId=\"3\" fillId=\"0\" borderId=\"0\"/><xf numFmtId=\"49\" fontId=\"3\" fillId=\"4\" borderId=\"0\"/></cellXfs><cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles></styleSheet>";
 public void Dispose(){if(journal!=null){journal.Dispose();journal=null;}}
}
}
