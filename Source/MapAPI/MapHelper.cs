using System;
using System.Collections.Generic;
using System.Text;
using MapInfo.Data;
using MapInfo.Engine;
using MapInfo.Geometry;
using MapInfo.Mapping;
using MapInfo.Windows.Controls;
using System.Drawing;
using MapInfo.Tools;
using MapInfo.Styles;
using System.Windows.Forms;

namespace MapAPI
{
    public class MapHelper
    {
        #region 线路定位
        /// <summary>
        /// 根据线路起始位置终止位置进行定位
        /// </summary>
        /// <param name="RoadTable">线路图层</param>
        /// <param name="StartLength">开始米数</param>
        /// <param name="EndLength">结束米数</param>
        /// <param name="MyMapControl">当前地图控件</param>
        public static void GetSegment(Table RoadTable, double StartLength, double EndLength, String RoadName,MapControl MyMapControl)
        {
            #region 获取临时图层并清空数据，如果图层不存在则创建
            CoordSys coordSys=MyMapControl.Map.GetDisplayCoordSys();
            Table tTemp = GetTempTable(MyMapControl);//创建或获取内存图层表，用于显示
            #endregion

            #region 如果其实里程大于终止里程 进行交换。
            if (StartLength > EndLength)
            {
                double temp = StartLength;
                StartLength = EndLength;
                EndLength = temp;
            }
            #endregion

            #region 查找线路所有的线路段 并进行排序
            IResultSetFeatureCollection segCollection = GetSegmentCollection(RoadTable, RoadName);
            if (segCollection == null || segCollection.Count <= 0)
            {
                throw new Exception("线路信息不存在！");
            }
            #endregion

            #region 寻找开始端和结束端
            List<Feature> ListRoadSegment = new List<Feature>();//用于保存所有属于区间范围的道路段
            bool bFindStart = false;//是否已经找到开始段
            bool bFindEnd = false;//是否已经找到结束段

            bool bStarZero=false; //线路起始即时起点
            bool bEndZero=false;//线路终点既是终点
            foreach (Feature f in segCollection)
            {
                //起点终点找到停止循环
                if (bFindStart && bFindEnd)
                    break;

                int Start = Convert.ToInt32(f["起点"]);
                int End = Convert.ToInt32(f["终点"]);
                bool bAdd = false;//当前已经添加

                if(EndLength<Start)
                {
                    break;
                }

                //如果起点未找到找起点
                if (!bFindStart)
                {
                    if (Start > StartLength || (StartLength >= Start && StartLength<=End))
                    {
                        //线路起始即时起点
                        bStarZero=true;
                        bFindStart=true;
                        ListRoadSegment.Add(f);
                        bAdd = true;
                    }
                    else
                    {
                        //开始未找到，找开始
                        if (StartLength >= Start && StartLength <= End)
                        {
                            bFindStart = true;
                            ListRoadSegment.Add(f);
                            bAdd = true;
                        }
                    }
                }

                //起点已找到找终点
                if (bFindStart)
                {
                    if (!bFindEnd)
                    {
                        if(EndLength<=End)
                        {
                            bFindEnd = true;
                            bEndZero = true;
                            //线路终点既是终点
                            if (EndLength >= Start)
                            {
                                if (!bAdd)
                                {
                                    ListRoadSegment.Add(f);
                                }
                            }
                            else
                            {
                                break;//查找结束
                            }
                        }
                        else
                        {
                           
                            //起点找到 终点未找到
                            if (EndLength > End || (EndLength >= Start && EndLength <= End))
                            {
                                if (!bAdd)
                                {
                                    ListRoadSegment.Add(f);
                                }
                                if ((EndLength >= Start && EndLength <= End))
                                {
                                    bFindEnd = true;
                                }
                            }
                            
                        }
                    }
                }
            }

            if (!bFindStart)
            {
                throw new Exception("查找起点终点信息失败！");
            }
            #endregion

            #region 处理线段构造新的线段

            if (ListRoadSegment.Count <= 0)
            {
                throw new Exception("定位线路信息出错");
            }

            List<DPoint> listPoint = new List<DPoint>();//保存线路所有的点
            double Length = 0;
            if (ListRoadSegment.Count == 1)
            {
                //单光缆段定位
                int SegIndex1 = 0;
                int SegIndex2 = 0;
                Length = (ListRoadSegment[0].Geometry as MultiCurve).Length(DistanceUnit.Meter);

                String Message = "类别:" + ListRoadSegment[0]["类别"];

                Feature fTemp = ListRoadSegment[0];
                double MiddleLength1 = 0;
                if (StartLength > Convert.ToInt32(ListRoadSegment[0]["起点"]))
                {
                    MiddleLength1 = (StartLength - Convert.ToDouble(ListRoadSegment[0]["起点"]))*Length / (Convert.ToDouble(ListRoadSegment[0]["终点"]) - Convert.ToDouble(ListRoadSegment[0]["起点"]));
                    
                }
                DPoint dpStartPoint = GetPoint(MiddleLength1, fTemp, out SegIndex1);

                
                double MiddleLength2 = Length;
                
                if (EndLength < Convert.ToInt32(ListRoadSegment[0]["终点"]))
                {
                    MiddleLength2 = ( EndLength- Convert.ToDouble(ListRoadSegment[0]["起点"]))*Length / (Convert.ToDouble(ListRoadSegment[0]["终点"]) - Convert.ToDouble(ListRoadSegment[0]["起点"]));

                }

                DPoint dpEndPoint = GetPoint(MiddleLength2, fTemp, out SegIndex2);

                listPoint.Add(dpStartPoint);

                MultiCurve mc = fTemp.Geometry as MultiCurve;
                for (int i = SegIndex1; i < SegIndex2; i++)
                {
                    listPoint.Add(mc[i].EndPoint);
                }
                listPoint.Add(dpEndPoint);

                CreateLineFeature(listPoint, tTemp, coordSys, Message);

            }
            else
            {
                //多光缆段定位
                for (int i=0;i<=ListRoadSegment.Count-1;i++)
                {
                    Length = (ListRoadSegment[i].Geometry as MultiCurve).Length(DistanceUnit.Meter);
                    double TempStart = Convert.ToInt32(ListRoadSegment[i]["起点"]);
                    double TempEnd = Convert.ToDouble(ListRoadSegment[i]["终点"]);
                    String Message = "类别:" + ListRoadSegment[i]["类别"];
                    if (i == 0)
                    {
                        //开始定位点
                        int SegIndex = 0;
                        double MiddleLength = 0;
                        if (StartLength > TempStart)
                        {
                            MiddleLength = (StartLength - TempStart) * Length / (TempEnd - TempStart);

                        }
                        DPoint dpStartPoint = GetPoint(MiddleLength, ListRoadSegment[i], out SegIndex);


                        //梳理需要绘制的点
                        //to do

                        //梳理点调用CreateLineFeature创建图元

                        listPoint.Add(dpStartPoint);
                        MultiCurve mc = ListRoadSegment[i].Geometry as MultiCurve;
                        for (int j = SegIndex; j < mc.CurveCount; j++)
                        {
                            listPoint.Add(mc[j].EndPoint);
                        }


                        CreateLineFeature(listPoint, tTemp, coordSys, Message);


                    }
                    else if (i == ListRoadSegment.Count - 1)
                    {
                        //结束定位点
                        int SegIndex = 0;
                        double MiddleLength =Length;
                        if (EndLength < TempEnd)
                        {
                            MiddleLength = (EndLength - TempStart) * Length / (TempEnd - TempStart);

                        }
                        DPoint dpStartPoint = GetPoint(MiddleLength, ListRoadSegment[i], out SegIndex);

                        //梳理需要绘制的点
                        //to do

                        //梳理点调用CreateLineFeature创建图元

                        
                        MultiCurve mc = ListRoadSegment[i].Geometry as MultiCurve;
                        for (int j = 0; j < SegIndex; j++)
                        {
                            listPoint.Add(mc[j].EndPoint);
                        }
                        listPoint.Add(dpStartPoint);

                        CreateLineFeature(listPoint, tTemp, coordSys, Message);
                    }
                    else
                    {
                        //添加所有点到LIST

                        //梳理需要绘制的点
                        //to do

                        //梳理点调用CreateLineFeature创建图元

                        MultiCurve mc = ListRoadSegment[i].Geometry as MultiCurve;
                        for (int j = 0; j < mc.CurveCount; j++)
                        {
                            listPoint.Add(mc[j].EndPoint);
                        }
                        CreateLineFeature(listPoint, tTemp, coordSys, Message);
                    }
                }
            }
            #endregion 

        }

        /// <summary>
        /// 查询该线路的所有线段并按照起点由小到大进行排序
        /// </summary>
        /// <param name="RoadTable">线路图层</param>
        /// <param name="RoadName">线别</param>
        /// <param name="RoadType">行别</param>
        /// <returns></returns>
        private static IResultSetFeatureCollection GetSegmentCollection(Table RoadTable, String RoadName)
        {
            //查询该线路的所有线段并按照起点由小到大进行排序
            string Where = String.Format("线名='{0}' order by 起点 asc", RoadName);
            SearchInfo si = MapInfo.Data.SearchInfoFactory.SearchWhere(Where);
            si.QueryDefinition.Columns = null;
            IResultSetFeatureCollection ifs = MapInfo.Engine.Session.Current.Catalog.Search(RoadTable, si);
            //Session.Current.Selections.DefaultSelection.Clear();
            //Session.Current.Selections.DefaultSelection.Add(ifs);
            return ifs;
        }

        #region 需要用到的常用算法
        /// <summary>
        /// 计算当前线段上的点
        /// </summary>
        /// <param name="MiddleLen">起点到该点的距离</param>
        /// <param name="fRoad">折线图源</param>
        /// <param name="CIncex">点所在的分线段序列号</param>
        /// <returns></returns>
        private static DPoint GetPoint(Double MiddleLen, Feature fRoad, out int CIncex)
        {
            double PLength = 0;


            double TotalLength = 0;//线路真实长度

            try
            {
                TotalLength = (fRoad.Geometry as MultiCurve).Length(DistanceUnit.Meter);//读取属性星系
            }
            catch
            { }
            PLength = (fRoad.Geometry as MapInfo.Geometry.MultiCurve).Length(MapInfo.Geometry.DistanceUnit.Meter);//米
            if (TotalLength == 0)
            {
                //属性信息中的长度信息部存在则直接取线路计算长度
                TotalLength = PLength;//米
            }
            if (PLength <= 0)
            {
                //线路长度数据错误则直接取返回值
                throw new Exception("线路长度存在问题！");
            }

            //分析中间点应该定位到那个段上
            MapInfo.Geometry.MultiCurve mc = fRoad.Geometry as MapInfo.Geometry.MultiCurve;

            //DPoint dpstart = mc[0].StartPoint;
            //DPoint dpEnd2= mc[0].EndPoint;
            //取线路上的所以后控制点用以计算直线长度
            DPoint[] dpallNodes = mc[0].SamplePoints();

            if (dpallNodes.Length <= 1)
            {
                throw new Exception("线路数据存在问题！");
            }

            DPoint dpStart = new DPoint();//开始点坐标
            DPoint dpEnd = new DPoint();//终止点坐标
            double segLength = 0; //当前线段长度
            double TempLen = 0;//加上所在点线长
            double StartLen = 0;

            double a = 0;//前半段比例
            double b = 1;//后半段比例

            //double tttt = 0;
            //for (int i = 0; i < dpallNodes.Length - 1; i++)
            //{
            //    tttt+= this.GetDistance(dpallNodes[i], dpallNodes[i + 1], fRoad.Geometry.CoordSys);
            //}
            CIncex = -1;
            if (dpallNodes.Length == 1)
            {
                dpStart = mc[0].StartPoint;
                dpEnd = mc[0].EndPoint;
                a = MiddleLen / TotalLength;
                b = (TotalLength - MiddleLen) / TotalLength;
                CIncex = 0;
            }
            else
            {
                for (int i = 0; i < dpallNodes.Length - 1; i++)
                {
                    double tempSegLength = GetDistance(dpallNodes[i], dpallNodes[i + 1], fRoad.Geometry.CoordSys);
                    StartLen = TempLen;
                    segLength = tempSegLength;
                    TempLen += tempSegLength;
                    if (TempLen / PLength >= MiddleLen / TotalLength)
                    {
                        dpStart = dpallNodes[i];
                        dpEnd = dpallNodes[i + 1];
                        CIncex = i;
                        break;
                    }

                }
                a = MiddleLen / TotalLength - StartLen / PLength;
                b = (TotalLength - MiddleLen) / TotalLength - (PLength - StartLen - segLength) / PLength;
            }

            return GetPointSub(dpStart, dpEnd, a, b);
        }

        private static DPoint GetPointSub(DPoint DpStart, DPoint DpEnd, double xLength, double yLength)
        {
            //double xLength = MiddleLen - StartLen;
            //double yLength = SegLength - xLength;
            double dPosX = (DpEnd.x - DpStart.x) * xLength / (xLength + yLength) + DpStart.x;
            double dPosY = (DpEnd.y * xLength + DpStart.y * yLength) / (yLength + xLength);
            return new DPoint(dPosX, dPosY);
        }

        private static double GetDistance(DPoint DpStart, DPoint DpEnd, CoordSys cs)
        {
            return MapInfo.Geometry.CoordSys.Distance(DistanceType.Cartesian, DistanceUnit.Meter, cs, DpStart, DpEnd);//(MapInfo.Geometry.DistanceType.Cartesian, mapControl1.Map.Zoom.Unit, mapControl1.Map.GetDisplayCoordSys(), dptStart, e.MapCoordinate);
        }
        #endregion

        /// <summary>
        /// 获取或者创建地图临时图层，用于显示定位标记
        /// </summary>
        /// <param name="MyMapControl"></param>
        /// <returns></returns>
        public static Table GetTempTable(MapControl MyMapControl)
        {
            //(tblTemp as ITableFeatureCollection).Clear(); 清除图层的方法
            MapInfo.Data.Table table = MapInfo.Engine.Session.Current.Catalog.GetTable("BugTable");//确保当前目录下不存在同名表
            if (table == null)
            {
                MapInfo.Data.TableInfoMemTable tblInfo = new MapInfo.Data.TableInfoMemTable("BugTable");
                tblInfo.Columns.Add(MapInfo.Data.ColumnFactory.CreateFeatureGeometryColumn(MyMapControl.Map.GetDisplayCoordSys()));//向表信息中添加必备的可绘图列
                tblInfo.Columns.Add(MapInfo.Data.ColumnFactory.CreateStyleColumn());
                //tblInfo.Columns.Add(MapInfo.Data.ColumnFactory.CreateIntColumn("index"));
                //tblInfo.Columns.Add(MapInfo.Data.ColumnFactory.CreateIntColumn("id"));
                //tblInfo.Columns.Add(MapInfo.Data.ColumnFactory.CreateStringColumn("type", 10));
                //tblInfo.Columns.Add(MapInfo.Data.ColumnFactory.CreateStringColumn("pointid", 50));
                //向表信息中添加索引列，用来查找
                //tblInfo.Columns.Add(MapInfo.Data.ColumnFactory.CreateStringColumn("namenull", 1));  //创建字符串型的列,用于标注
                tblInfo.Columns.Add(MapInfo.Data.ColumnFactory.CreateStringColumn("name", 50));  //创建字符串型的列,用于标注
                //tblInfo.Columns.Add(MapInfo.Data.ColumnFactory.CreateStringColumn("nodeid", 50));  //创建字符串型的列,用于标注
                tblInfo.Columns.Add(MapInfo.Data.ColumnFactory.CreateStringColumn("other", 500));  //创建字符串型的列,用于标注

                table = MapInfo.Engine.Session.Current.Catalog.CreateTable(tblInfo);//根据表信息创建临时表
                FeatureLayer bugLayer = new FeatureLayer(table, "lBugLayer", "lBug");//创建图层(并关联表)
                LabelSource source = new LabelSource(table);//绑定Table
                source.DefaultLabelProperties.Caption = "name";//指定哪个字段作为显示标注
                source.DefaultLabelProperties.Style.Font.ForeColor = Color.Red;
                source.DefaultLabelProperties.CalloutLine.Use = false;  //是否使用标注线  
                source.DefaultLabelProperties.Layout.Offset = 5;//标注偏移   
                LabelLayer labelLayer = new LabelLayer();
                labelLayer.Sources.Append(source);//加载指定数据
                MyMapControl.Map.Layers.Add(labelLayer);
                MyMapControl.Map.Layers.Add(bugLayer);
                MyMapControl.Tools["Select"].UseDefaultInfoTipLayerExpressions = false;
                MapTool.SetInfoTipExpression(MyMapControl.Tools["Select"], bugLayer, "'详细信息：'+other"); // "'详细信息：'+other" //MyMapControl.Tools.MapToolProperties
            }
            

            return table;
            
        }

        private static SimpleLineStyle vLine = new SimpleLineStyle(new LineWidth(3, LineWidthUnit.Point), 2, Color.Red);//默认线样式
        private static void CreateLineFeature(List<DPoint> ListDPoint,Table FeatureTable,CoordSys CoordSys,String Message)
        {
            //条件不足无法创建
            if (ListDPoint == null || ListDPoint.Count < 2 || FeatureTable==null||CoordSys==null)
                return;

            //创建并且将线添加到地图中去
            DPoint[] DpointArray=new DPoint[ListDPoint.Count];
            for(int i=0;i<ListDPoint.Count;i++)
            {
                DpointArray[i]=ListDPoint[i];
            }
            FeatureGeometry fg = new MapInfo.Geometry.MultiCurve(CoordSys, CurveSegmentType.Linear, DpointArray);
           
            //SimpleInterior vInter = new SimpleInterior(9, Color.Yellow, Color.Yellow, true);
            //CompositeStyle cStyle = new CompositeStyle(new AreaStyle(vLine, vInter), null, null, null);

            Feature fLine = new Feature(FeatureTable.TableInfo.Columns); //new Feature(fg, vLine);//创建图元对象
            fLine["name"] = String.Empty;
            fLine["other"] =  Message;
            fLine.Geometry = fg;
            fLine.Style = vLine;
            FeatureTable.InsertFeature(fLine);//加入到地图
        }
        #endregion

        #region 股道道岔
        /// <summary>
        /// 高亮股道道岔
        /// </summary>
        /// <param name="ID"></param>
        /// <param name="Type"></param>
        /// <param name="RoadTable">道路所在图层</param>
        public static void HighlightFeature(string ID, string Type, Table RoadTable, MapControl MyMapControl)
        {
            //查询该线路的所有线段并按照起点由小到大进行排序
            string Where = String.Format("编号='{0}' and 类别='{1}'", ID, Type);
            SearchInfo si = MapInfo.Data.SearchInfoFactory.SearchWhere(Where);
            si.QueryDefinition.Columns = null;
            IResultSetFeatureCollection ifs = MapInfo.Engine.Session.Current.Catalog.Search(RoadTable, si);

            if (ifs == null || ifs.Count <= 0)
                return;

            CoordSys coordSys = MyMapControl.Map.GetDisplayCoordSys();
            Table tTemp = GetTempTable(MyMapControl);//创建或获取内存图层表，用于显示

            //创建临时图元
            foreach (Feature f in ifs)
            {

                FeatureGeometry fg = f.Geometry;

                Feature fLine = new Feature(tTemp.TableInfo.Columns); //new Feature(fg, vLine);//创建图元对象
                fLine["name"] = ID;
                fLine["other"] = Type;
                fLine.Geometry = fg;
                fLine.Style = vLine;
                tTemp.InsertFeature(fLine);//加入到地图
            }
        }
        #endregion
    }
}
