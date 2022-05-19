using System;
using System.Collections.Generic;
using System.Text;
using System.Data.SqlClient;
using System.Data;
using System.Windows.Forms;

namespace MapAPI
{
    public class SQLHelper
    {
        private SqlConnection StyleConnection;

        #region 构造方法
        private SQLHelper()
        {
            #region 初始化连接信息
            StyleConnection = new SqlConnection(System.Configuration.ConfigurationManager.AppSettings["DB"]);
            #endregion
        }
        #endregion

        #region 单例
        private static SQLHelper _instance = null;

        /// <summary>
        /// PGHelper的实例
        /// </summary>
        public static SQLHelper Instance
        {
            get
            {
                if(_instance == null)
                {
                    _instance = new SQLHelper();
                }
                return _instance;
            }
        }
        #endregion

        #region 公有方法
        /// <summary>
        /// 从型号基础库获取数据
        /// </summary>
        /// <param name="SQL">查询的SQL语句</param>
        /// <returns>查询的结果</returns>
        public DataTable GetDataTable(String SQL)
        {
            SqlCommand cmd = new SqlCommand(SQL, StyleConnection);
            SqlDataAdapter da = new SqlDataAdapter(cmd);
            DataTable dt = new DataTable();
            da.Fill(dt);
            return dt;
        }
        #endregion

    }
}
