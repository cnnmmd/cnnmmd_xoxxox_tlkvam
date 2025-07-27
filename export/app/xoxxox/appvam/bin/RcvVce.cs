using System;
using System.Text;
using System.Threading;
using System.Net.Sockets;

using System.Collections;
using UnityEngine;
using Xoxxox;

namespace Xoxxox {
  public class RcvVce: MVRScript {

    // 変数を定義
    public string srvadr = Params.srvadr; // サーバのアドレス
    public int srvprt = Params.srvprt; // サーバのポート
    public string pthsnd = ParamsVce.pthsnd; // サーバのパス（送信用）
    public int ratsmp = 16000; // 音声のサンプル数／秒＝サンプルレート
    public int secchk = 5000; // 検査を待つ時間（マイクロ秒）

    private Thread trdbak; // スレッド（バックグラウンド）
    private AudioSource objawd; // デバイス（スピーカ）
    private float[] arrvce; // 音声アレイ
    private bool flgrcv = false; // 検査フラグ（音声を受信する可否）

    // 処理を開始
    private void Start() {
      trdbak = new Thread(ExeBak);
      trdbak.Start();
      objawd = containingAtom.GetComponentInChildren<AudioSource>();
    }

    // 処理を反復（フォアグラウンド）
    private void Update() {
      if (flgrcv == true) { // 検査フラグが真（音声を受信可能）なら
        try {
          AudioClip clpvce = AudioClip.Create("", arrvce.Length, 1, ratsmp, false);
          clpvce.SetData(arrvce, 0);
          objawd.clip = clpvce;
          objawd.Play();
        }
        catch (Exception e) {
          SuperController.LogError("RcvVce:err[" + e + "]"); // DBG
        }
        flgrcv = false; // 検査フラグを偽
      }
    }

    // 処理を反復（バックグラウンド）
    private void ExeBak() {
      while (true) {
        SuperController.LogMessage("RcvVce:bgn[]"); // DBG
        PolVce();
        SuperController.LogMessage("RcvVce:end[]"); // DBG
        flgrcv = true; // 検査フラグを真（音声が受信可能）
        Thread.Sleep(secchk); // 指定時間だけ待機
      }
    }

    // 音声を受信
    private void PolVce() {
      string strreq =
          $"POST {pthsnd} HTTP/1.1\r\n" +
          "Host: {srvadr}\r\n" +
          "Connection: Close\r\n\r\n";
      byte[] bytreq = Encoding.ASCII.GetBytes(strreq);

      using (TcpClient client = new TcpClient(srvadr, srvprt))
      using (NetworkStream stream = client.GetStream()) {
        stream.Write(bytreq, 0, bytreq.Length); // リクエストを送信（HTTP）
        byte[] arrbuf = new byte[2048];
        int posbuf;
        bool flghed = false;
        StringBuilder bldhed = new StringBuilder();
        byte[] bytvce = new byte[0];

        while ((posbuf = stream.Read(arrbuf, 0, arrbuf.Length)) > 0) {
          if (!flghed) {
            bldhed.Append(Encoding.ASCII.GetString(arrbuf, 0, posbuf));
            string strtmp = bldhed.ToString();
            int possep = strtmp.IndexOf("\r\n\r\n"); // ヘッダとボディの境界を検出
            if (possep >= 0) {
              flghed = true;
              int lenhed = Encoding.ASCII.GetByteCount(strtmp.Substring(0, possep + 4));
              bytvce = new byte[posbuf - lenhed];
              Array.Copy(arrbuf, lenhed, bytvce, 0, posbuf - lenhed);  // ヘッダを除いた部分を保存
            }
          }
          else { // すでに読み込んだ音声データに、新しく読み込んだ音声データを追加
            int lenvce = bytvce.Length;
            Array.Resize(ref bytvce, lenvce + posbuf);
            Array.Copy(arrbuf, 0, bytvce, lenvce, posbuf);
          }
        }
        if (bytvce.Length > 0) { // 音声データを再生用（AudioClip ）に変換
          arrvce = new float[bytvce.Length / 4];
          Buffer.BlockCopy(bytvce, 0, arrvce, 0, bytvce.Length);
        }
      }
    }

    // 処理を終了
    private void OnDestroy() {
      if (trdbak != null && trdbak.IsAlive) {
        trdbak.Abort();
      }
    }
  }
}
