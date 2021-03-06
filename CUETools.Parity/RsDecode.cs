using System;
using System.Collections.Generic;
using System.Text;

namespace CUETools.Parity
{
	/**
	 * タイトル: RSコード・デコーダ
	 *
	 * @author Masayuki Miyazaki
	 * http://sourceforge.jp/projects/reedsolomon/
	 */
	public class RsDecode
	{
		protected Galois galois;
		protected int npar;

		public RsDecode(int npar, Galois galois)
		{
			this.npar = npar;
			this.galois = galois;
        }

		/// <summary>
		/// Modified Berlekamp-Massey 
		/// </summary>
		/// <param name="sigma">
		/// σ(z)格納用配列、最大npar/2 + 2個の領域が必要
		/// σ0,σ1,σ2, ... σ[jisu]
		/// </param>
		/// <param name="omega">
		/// ω(z)格納用配列、最大npar/2 + 1個の領域が必要
		/// ω0,ω1,ω2, ... ω[jisu-1]
		/// </param>
		/// <param name="syn">
		/// シンドローム配列
		/// s0,s1,s2, ... s[npar-1]
		/// </param>
		/// <returns>
		/// >= 0: σの次数
		/// else: エラー
		/// </returns>
		public unsafe int calcSigmaMBM(int* sigma, int* syn)
		{
			int* sg0 = stackalloc int[npar + 1];
			int* sg1 = stackalloc int[npar + 1];
			int* wk = stackalloc int[npar + 1];

			sg0[1] = 1;
			sg1[0] = 1;
			int jisu0 = 1;
			int jisu1 = 0;
			int m = -1;

			for (int n = 0; n < npar; n++)
			{
				// 判別式を計算
				int d = syn[n];
				for (int i = 1; i <= jisu1; i++)
					d ^= galois.mul(sg1[i], syn[n - i]);

				if (d != 0)
				{
					int logd = galois.toLog(d);
					for (int i = 0; i <= n; i++)
						wk[i] = sg1[i] ^ galois.mulExp(sg0[i], logd);
					int js = n - m;
					if (js > jisu1)
					{
						for (int i = 0; i <= jisu0; i++)
							sg0[i] = galois.divExp(sg1[i], logd);
						m = n - jisu1;
						jisu1 = js;
						jisu0 = js;
					}
					for (int i = 0; i < npar; i++)
						sg1[i] = wk[i];
				}
				for (int i = jisu0; i > 0; i--)
					sg0[i] = sg0[i - 1];
				sg0[0] = 0;
				jisu0++;
			}
			if (sg1[jisu1] == 0)
				return -1;
			//galois.mulPoly(omega, sg1, syn, npar / 2 + 1, npar, npar);
			for (int i = 0; i < Math.Min(npar / 2 + 2, npar); i++)
				sigma[i] = sg1[i];
			return jisu1;
		}

		public unsafe int calcSigmaMBM(int[] sigma, int[] omega, int[] syn)
		{
			fixed (int* s = sigma, o = omega, y = syn)
			{
				int res = calcSigmaMBM(s, y);
				if (res >= 0) galois.mulPoly(o, s, y, npar / 2 + 1, npar, npar);
				return res;
			}
		}

		/**
		 * 最終エラー位置のセット
		 * @param pos
		 * 		誤り位置格納用初列
		 * @param n
		 * 		データ長
		 * @param last
		 * 		エラー位置
		 * @return
		 * 		0: 正常終了
		 * 		< 0: エラー
		 */
		private unsafe bool setLastErrorPos(int* pos, int n, int last)
		{
			if (galois.toLog(last) >= n)
				return false;	// 範囲外なのでエラー
			pos[0] = last;
			return true;
		}

		/**
		 * チェン探索により誤り位置を求める
		 *		σ(z) = 0の解を探索する
		 *		ただし、探索はデータ長以内の解のみで
		 *		jisu個の解が見つからなければ、エラーとする
		 * @param pos int[]
		 * 		誤り位置格納用配列、jisu個の領域が必要
		 * @param n int
		 * 		データ長
		 * @param jisu int
		 * 		σの次数
		 * @param sigma int[]
		 * 		σ0,σ1,σ2, ... σ<jisu>
		 * @return int
		 *		0: 正常終了
		 *		< 0: エラー
		 */
		public unsafe bool chienSearch(int* pos, int n, int jisu, int* sigma)
		{
			/*
			 * σ(z) = (1-α^i*z)(1-α^j*z)(1-α^k*z)
			 *       = 1 + σ1z + σ2z^2 +...
			 * σ1 = α^i + α^j + α^k
			 * つまりσ1は全ての解の合計となっている。上記の性質を利用して、プチ最適化
			 * last = σ1から、見つけた解を次々と引いていくことにより、最後の解はlastとなる
			 */
			int last = sigma[1];
			if (jisu == 1)
			{
				// 次数が1ならばlastがその解である
				return setLastErrorPos(pos, n, last);
			}
			int* sg = stackalloc int[jisu + 1];
			for (int j = 1; j <= jisu; j++)
				sg[j] = sigma[j];
			int posIdx = jisu - 1;		// 誤り位置格納用インデックス

            bool haveZeroes = false;
            // haveZeroes = true;
            for (int j = 1; j <= jisu; j++)
                haveZeroes |= sg[j] == 0;
            if (!haveZeroes && this.galois.Max == 0xffff)
            {
                const int himax = 0x11000;
                fixed (ushort* exp = this.galois.ExpTbl, log = this.galois.LogTbl)
                {
                    for (int j = 1; j <= jisu; j++)
                    {
                        sg[j] = log[sg[j]] - ((j * n) % 0xffff) + 0xffff;
                        sg[j] = (sg[j] & 0xffff) + (sg[j] >> 16);
                    }
                    int i = n;
                    while (i > 0)
                    {
                        int cnt = i;
                        for (int j = 1; j <= jisu; j++)
                        {
                            sg[j] = (sg[j] & 0xffff) + (sg[j] >> 16);
                            cnt = Math.Min(cnt, (himax - sg[j]) / j);
                        }
                        i -= RsDecode.chienFast(sg, exp, cnt, jisu);
                        int wk = 1;
                        for (int j = 1; j <= jisu; j++)
                            wk ^= exp[sg[j]];
                        if (wk == 0)
                        {
                            last ^= pos[posIdx--] = exp[i];
                            if (posIdx == 0)
                            {
                                pos[0] = last;
                                return log[last] < n;
                            }
                        }
                    }
                }
                return false;
            }

			for (int i = 0; i < n; i++)
			{
				/*
				 * σ(z)の計算
				 * w を1(0乗の項)に初期化した後、残りの項<1..jisu>を加算
				 * z = 1/α^i = α^Iとすると
				 * σ(z) = 1 + σ1α^I + σ2(α^I)^2 + σ3(α^I)^3 + ... + σ<jisu>/(α^I)^<jisu>
				 *      = 1 + σ1α^I + σ2α^(I*2) + σ3α^(I*3) + ... + σ<jisu>α^(I*<jisu>)
				 */
				int wk = 1;
				for (int j = 1; j <= jisu; j++)
					wk ^= sg[j];
				for (int j = 1; j <= jisu; j++)
				{
					sg[j] = galois.divExp(sg[j], j);
				}
				if (wk == 0)
				{
					int pv = galois.toExp(i);		// σ(z) = 0の解
					last ^= pv;					// lastから今見つかった解を引く
					pos[posIdx--] = pv;
					if (posIdx == 0)
					{
						// 残りが一つならば、lastがその解である
						return setLastErrorPos(pos, n, last);
					}
				}
			}
			// 探索によりデータ長以内に、jisu個の解が見つからなかった
			return false;
		}

		public unsafe bool chienSearch(int[] pos, int n, int jisu, int[] sigma)
		{
			fixed (int* p = pos, s = sigma)
				return chienSearch(p, n, jisu, s);
		}

		public unsafe int doForney(int jisu, int ps, int* sigma, int* omega)
		{
			int zlog = galois.Max - galois.toLog(ps);					// zのスカラー

			// ω(z)の計算
			int ov = omega[0];
			for (int j = 1; j < jisu; j++)
			{
				ov ^= galois.mulExp(omega[j], (zlog * j) % galois.Max);		// ov += ωi * z^j
			}

			// σ'(z)の値を計算(σ(z)の形式的微分)
			int dv = sigma[1];
			for (int j = 2; j < jisu; j += 2)
			{
				dv ^= galois.mulExp(sigma[j + 1], (zlog * j) % galois.Max);	// dv += σ<j+1> * z^j
			}

			/*
			 * 誤り訂正 E^i = α^i * ω(z) / σ'(z)
			 * 誤り位置の範囲はチェン探索のときに保証されているので、
			 * ここではチェックしない
			 */
			return galois.mul(ps, galois.div(ov, dv));
		}

        private unsafe static int chienFast(int* sg, ushort* exp, int cnt, int jisu)
        {
            int start = cnt;
            switch (jisu)
            {
                case 2:
                    {
                        int sg1 = sg[1];
                        int sg2 = sg[2];
                        do
                        {
                            sg1 += 1;
                            sg2 += 2;
                        }
                        while (--cnt > 0 && (exp[sg1] ^ exp[sg2]) != 1);
                        sg[1] = sg1;
                        sg[2] = sg2;
                    }
                    break;
                case 3:
                    {
                        int sg1 = sg[1];
                        int sg2 = sg[2];
                        int sg3 = sg[3];
                        do
                        {
                            sg1 += 1;
                            sg2 += 2;
                            sg3 += 3;
                        }
                        while (--cnt > 0 && (exp[sg1] ^ exp[sg2] ^ exp[sg3]) != 1);
                        sg[1] = sg1;
                        sg[2] = sg2;
                        sg[3] = sg3;
                    }
                    break;
                case 4:
                    {
                        int sg1 = sg[1];
                        int sg2 = sg[2];
                        int sg3 = sg[3];
                        int sg4 = sg[4];
                        do
                        {
                            sg1 += 1;
                            sg2 += 2;
                            sg3 += 3;
                            sg4 += 4;
                        }
                        while (--cnt > 0 && (exp[sg1] ^ exp[sg2] ^ exp[sg3] ^ exp[sg4]) != 1);
                        sg[1] = sg1;
                        sg[2] = sg2;
                        sg[3] = sg3;
                        sg[4] = sg4;
                    }
                    break;
                default:
                    {
                        int sg1 = sg[1];
                        int sg2 = sg[2];
                        int sg3 = sg[3];
                        int sg4 = sg[4];
                        int sg5 = sg[5];
                        int wkhi;
                        do
                        {
                            wkhi = exp[sg1 += 1] ^ exp[sg2 += 2] ^ exp[sg3 += 3] ^ exp[sg4 += 4] ^ exp[sg5 += 5];
                            for (int j = 6; j <= jisu; j++)
                                wkhi ^= exp[sg[j] += j];
                        }
                        while (--cnt > 0 && wkhi != 1);
                        sg[1] = sg1;
                        sg[2] = sg2;
                        sg[3] = sg3;
                        sg[4] = sg4;
                        sg[5] = sg5;
                    }
                    break;
            }
            return start - cnt;
        }
	}

	public class RsDecode8: RsDecode
	{
		public RsDecode8(int npar)
			: base(npar, Galois81D.instance)
		{
		}

		/**
		 * Forney法で誤り訂正を行う
		 *		σ(z) = (1-α^i*z)(1-α^j*z)(1-α^k*z)
		 *		σ'(z) = α^i * (1-α^j*z)(1-α^k*z)...
		 *			   + α^j * (1-α^i*z)(1-α^k*z)...
		 *			   + α^k * (1-α^i*z)(1-α^j*z)...
		 *		ω(z) = (E^i/(1-α^i*z) + E^j/(1-α^j*z) + ...) * σ(z)
		 *			  = E^i*(1-α^j*z)(1-α^k*z)...
		 *			  + E^j*(1-α^i*z)(1-α^k*z)...
		 *			  + E^k*(1-α^i*z)(1-α^j*z)...
		 *		∴ E^i = α^i * ω(z) / σ'(z)
		 * @param data int[]
		 *		入力データ配列
		 * @param length int
		 *		入力データ長さ
		 * @param jisu int
		 *		σの次数
		 * @param pos int[]
		 *		誤り位置配列
		 * @param sigma int[]
		 *		σ0,σ1,σ2, ... σ<jisu>
		 * @param omega int[]
		 *		ω0,ω1,ω2, ... ω<jisu-1>
		 */
		private unsafe void doForney(byte[] data, int length, int jisu, int[] pos, int[] sigma, int[] omega)
		{
			fixed (int* s = sigma, o = omega)
				for (int i = 0; i < jisu; i++)
				{
					int ps = pos[i];
					data[galois.toPos(length, ps)] ^= (byte)doForney(jisu, ps, s, o);
				}
		}

		/**
		 * RSコードのデコード
		 *
		 * @param data int[]
		 *		入力データ配列
		 * @param length int
		 * 		パリティを含めたデータ長
		 * @param noCorrect boolean
		 * 		チェックのみで訂正は行わない
		 * @return bool
		 * 		0: エラーなし
		 * 		> 0: 戻り値個の誤りを訂正した
		 * 		< 0: 訂正不能
		 */
		public bool decode(byte[] data, int length, bool noCorrect, out int errors)
		{
			if (length < npar || length > galois.Max)
				throw new Exception("RsDecode: wrong length");

			errors = 0;

			// シンドロームを計算
			int[] syn = new int[npar];
			if (galois.calcSyndrome(data, length, syn))
				return true; // エラー無し

			// シンドロームよりσとωを求める
			int[] sigma = new int[npar / 2 + 2];
			int[] omega = new int[npar / 2 + 1];
			int jisu = calcSigmaMBM(sigma, omega, syn);
			if (jisu <= 0)
				return false;
			
			// チェン探索により誤り位置を求める
			int[] pos = new int[jisu];
			if (!chienSearch(pos, length, jisu, sigma))
				return false;
			if (!noCorrect) // 誤り訂正
				doForney(data, length, jisu, pos, sigma, omega);
			errors = jisu;
			return true;
		}
	}

	public class RsDecode16 : RsDecode
	{
		public RsDecode16(int npar, Galois galois)
			: base(npar, galois)
		{
		}

		public RsDecode16(int npar)
			: this(npar, Galois16.instance)
		{
		}

		/**
		 * Forney法で誤り訂正を行う
		 *		σ(z) = (1-α^i*z)(1-α^j*z)(1-α^k*z)
		 *		σ'(z) = α^i * (1-α^j*z)(1-α^k*z)...
		 *			   + α^j * (1-α^i*z)(1-α^k*z)...
		 *			   + α^k * (1-α^i*z)(1-α^j*z)...
		 *		ω(z) = (E^i/(1-α^i*z) + E^j/(1-α^j*z) + ...) * σ(z)
		 *			  = E^i*(1-α^j*z)(1-α^k*z)...
		 *			  + E^j*(1-α^i*z)(1-α^k*z)...
		 *			  + E^k*(1-α^i*z)(1-α^j*z)...
		 *		∴ E^i = α^i * ω(z) / σ'(z)
		 * @param data int[]
		 *		入力データ配列
		 * @param length int
		 *		入力データ長さ
		 * @param jisu int
		 *		σの次数
		 * @param pos int[]
		 *		誤り位置配列
		 * @param sigma int[]
		 *		σ0,σ1,σ2, ... σ<jisu>
		 * @param omega int[]
		 *		ω0,ω1,ω2, ... ω<jisu-1>
		 */
		private unsafe void doForney(ushort* data, int length, int jisu, int[] pos, int[] sigma, int[] omega)
		{
			fixed (int* s = sigma, o = omega)
				for (int i = 0; i < jisu; i++)
				{
					int ps = pos[i];
					data[galois.toPos(length, ps)] ^= (ushort)doForney(jisu, ps, s, o);
				}
		}

		/**
		 * RSコードのデコード
		 *
		 * @param data int[]
		 *		入力データ配列
		 * @param length int
		 * 		パリティを含めたデータ長
		 * @param noCorrect boolean
		 * 		チェックのみで訂正は行わない
		 * @return bool
		 * 		0: エラーなし
		 * 		> 0: 戻り値個の誤りを訂正した
		 * 		< 0: 訂正不能
		 */
		public unsafe bool decode(ushort* data, int length, bool noCorrect, out int errors)
		{
			if (length < npar || length > galois.Max)
				throw new Exception("RsDecode: wrong length");

			errors = 0;

			// シンドロームを計算
			int[] syn = new int[npar];
			if (galois.calcSyndrome(data, length, syn))
				return true; // エラー無し

			// シンドロームよりσとωを求める
			int[] sigma = new int[npar / 2 + 2];
			int[] omega = new int[npar / 2 + 1];
			int jisu = calcSigmaMBM(sigma, omega, syn);
			if (jisu <= 0)
				return false;

			// チェン探索により誤り位置を求める
			int[] pos = new int[jisu];
			if (!chienSearch(pos, length, jisu, sigma))
				return false;
			if (!noCorrect) // 誤り訂正
				doForney(data, length, jisu, pos, sigma, omega);
			errors = jisu;
			return true;
		}

		public unsafe bool decode(byte[] data, int datapos, int length, bool noCorrect, out int errors)
		{
			if ((length & 1) != 0)
				throw new Exception("RsDecode: wrong length");
			fixed (byte* bytes = &data[datapos])
				return decode((ushort*)bytes, length >> 1, noCorrect, out errors);
		}
	}
}
