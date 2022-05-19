using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.IO;
using MapInfo.Mapping;
using MapInfo.Data;
using MapInfo.Engine;
using MapAPI;

namespace MapTestApp
{
    public partial class MapForm1 : Form
    {
        public MapForm1()
        {
            InitializeComponent();
            mapControl1.Map.ViewChangedEvent += new MapInfo.Mapping.ViewChangedEventHandler(Map_ViewChangedEvent);
            Map_ViewChangedEvent(this, null);
        }

        void Map_ViewChangedEvent(object sender, MapInfo.Mapping.ViewChangedEventArgs e)
        {
            // Display the zoom level
            Double dblZoom = System.Convert.ToDouble(String.Format("{0:E2}", mapControl1.Map.Zoom.Value));
            if (statusStrip1.Items.Count > 0)
            {
                statusStrip1.Items[0].Text = "缩放: " + dblZoom.ToString() + " " + MapInfo.Geometry.CoordSys.DistanceUnitAbbreviation(mapControl1.Map.Zoom.Unit);
            }
        }

        private void MapForm1_Load(object sender, EventArgs e)
        {
            //加载地图
            string MapPath = Path.Combine(Application.StartupPath, @"map\map.mws");
            MapWorkSpaceLoader mwsLoader = new MapWorkSpaceLoader(MapPath);
            mapControl1.Map.Load(mwsLoader);
            mapControl1.Tools.LeftButtonTool = "Select";
        }

        private void button1_Click(object sender, EventArgs e)
        {

            //清除
            Table tTemp = MapHelper.GetTempTable(this.mapControl1);//创建或获取内存图层表，用于显示
            (tTemp as ITableFeatureCollection).Clear();//清楚标记内容

            Table tRoad = Session.Current.Catalog.GetTable("railway");
            //查询数据
            DataTable dt = SQLHelper.Instance.GetDataTable("select * from 表");
            List<MyEntity> listMyEntity = new List<MyEntity>();
            foreach (DataRow dr in dt.Rows)
            {
                try
                {
                    MyEntity entity = new MyEntity();
                    entity.ID = dr["id"].ToString();
                    entity.LineName = dr["线别"].ToString();
                    entity.Type = dr["行别"].ToString();
                    entity.Zone = dr["区间或车站"].ToString();
                    entity.Position = dr["里程或位置"].ToString();
                    entity.Window = dr["天窗"].ToString();
                    entity.TimeZone = dr["时间段"].ToString();
                    listMyEntity.Add(entity);
                }
                catch
                { }
            }

            //处理数据
            foreach (MyEntity entity in listMyEntity)
            {
                try
                {
                    if (entity.Position.Contains("~"))
                    {
                        //当前为正线 需要定位
                        string[] sTempArr = entity.Position.Split('~');
                        if (sTempArr.Length < 2)
                            continue;
                        else
                        {
                            double start=Convert.ToDouble(sTempArr[0])*1000;
                            double end = Convert.ToDouble(sTempArr[1])*1000;
                            string RoadName = entity.LineName;
                            MapAPI.MapHelper.GetSegment(tRoad, start, end, RoadName, this.mapControl1);

                        }
                    }
                    else if (entity.Position.Contains("道岔"))
                    {
                        //处理道岔
                        string[] sTempArr = entity.Position.Split('、');
                        foreach (String stemp in sTempArr)
                        {
                            string ID = stemp.Replace("道岔", "");
                            MapHelper.HighlightFeature(ID, "道岔", tRoad,this.mapControl1);
                        }
                    }
                    else if (entity.Position.Contains("股道"))
                    {
                        //处理股道
                        string[] sTempArr = entity.Position.Split('、');
                        foreach (String stemp in sTempArr)
                        {
                            string ID = stemp.Replace("处理股道","");
                            MapHelper.HighlightFeature(ID, "股道", tRoad, this.mapControl1);
                        }
                    }
                }
                catch
                { }
            }

            
            //int start = (int)numericUpDown1.Value;
            //int end = (int)numericUpDown2.Value;
            ////try
            ////{
            //    MapAPI.MapHelper.GetSegment(tRoad, start, end, "沈山下行线", this.mapControl1);
            ////}
            ////catch (Exception ex)
            ////{
            ////    MessageBox.Show("Err:" + ex.Message);
            ////}
            ////this.mapControl1.Update();
        }
    }
}
