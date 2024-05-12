# NodoAme source code

[日本語マニュアル](https://inuinu2022.github.io/NodoAme.Home/)

## How to build

1. Install [.NET SDK](https://dotnet.microsoft.com/download)
2. `git clone https://github.com/InuInu2022/NodoAme.Core.git --recursive`
3. Download dic files to `dic/open_jtalk_dic_utf_8-1.11` directory
   1. Open JTalk dic files
      1. [source forge](http://downloads.sourceforge.net/open-jtalk/open_jtalk_dic_utf_8-1.11.tar.gz)
4. Download default voice files to `dic` directory
   1. default male jp voice files
      1. [nitech_jp_atr503_m001.htsvoice](https://sourceforge.net/projects/open-jtalk/files/HTS%20voice/hts_voice_nitech_jp_atr503_m001-1.05/)
   2. default female jp voice files
      1. [tohoku-f01](https://github.com/icn-lab/htsvoice-tohoku-f01)
5. `dotnet build`
   1. or `dotnet publish`