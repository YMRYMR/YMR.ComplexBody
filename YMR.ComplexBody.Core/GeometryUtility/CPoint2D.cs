using System;

namespace YMR.ComplexBody.Core.GeometryUtility
{
	/// <summary>
	/// Summary description for CPoint2D.
	/// </summary>
	
	//A point in Coordinate System
	public class CPoint2D
	{
		private double m_dCoordinate_X;
		private double m_dCoordinate_Y;

        public CPoint2D() { }
		
		public CPoint2D(double xCoordinate, double yCoordinate)
		{
			m_dCoordinate_X=xCoordinate;
			m_dCoordinate_Y=yCoordinate;
		}

		public double X
		{
			set
			{
				m_dCoordinate_X=value;
			}
			get
			{
				return m_dCoordinate_X;
			}
		}

		public double Y
		{
			set
			{
				m_dCoordinate_Y=value;
			}
			get
			{
				return m_dCoordinate_Y;
			}
		}

		public static bool SamePoints(CPoint2D Point1,
			CPoint2D Point2)
		{
		
			double dDeff_X=
				Math.Abs(Point1.X-Point2.X);
			double dDeff_Y=
				Math.Abs(Point1.Y-Point2.Y);

			if ((dDeff_X<ConstantValue.SmallValue)
				&& (dDeff_Y<ConstantValue.SmallValue))
				return true;
			else
				return false;
		}
		
		public bool EqualsPoint(CPoint2D newPoint)
		{
		
			double dDeff_X=
				Math.Abs(m_dCoordinate_X-newPoint.X);
			double dDeff_Y=
				Math.Abs(m_dCoordinate_Y-newPoint.Y);

			if ((dDeff_X<ConstantValue.SmallValue)
				&& (dDeff_Y<ConstantValue.SmallValue))
				return true;
			else
				return false;

		}

		/***To check whether the point is in a line segment***/
		public bool InLine(CLineSegment lineSegment)
		{
			bool bInline=false;

			double Ax, Ay, Bx, By, Cx, Cy;
			Bx=lineSegment.EndPoint.X;
			By=lineSegment.EndPoint.Y;
			Ax=lineSegment.StartPoint.X;
			Ay=lineSegment.StartPoint.Y;
			Cx=this.m_dCoordinate_X;
			Cy=this.m_dCoordinate_Y;
  
			double L=lineSegment.GetLineSegmentLength();
			double s=Math.Abs(((Ay-Cy)*(Bx-Ax)-(Ax-Cx)*(By-Ay))/(L*L));
  
			if (Math.Abs(s-0)<ConstantValue.SmallValue)
			{
				if ((SamePoints(this, lineSegment.StartPoint)) ||
					(SamePoints(this, lineSegment.EndPoint)))
					bInline=true;
				else if ((Cx<lineSegment.GetXmax())
					&& (Cx>lineSegment.GetXmin())
					&&(Cy< lineSegment.GetYmax())
					&& (Cy>lineSegment.GetYmin()))
					bInline=true;
			}
			return bInline;
		}

		/*** Distance between two points***/
		public double DistanceTo(CPoint2D point)
		{
			return Math.Sqrt((point.X-this.X)*(point.X-this.X) 
				+ (point.Y-this.Y)*(point.Y-this.Y));

		}

        /*
         * Alternative implementation from http://alienryderflex.com/polygon/
         */
        public bool PointInsidePolygon(CPoint2D[] polygonVertices)
        {
            if (polygonVertices.Length < 3) //not a valid polygon
                return false;

            int nCounter = 0;
            int nPoints = polygonVertices.Length;
            bool oddNodes = false;
            int j = nPoints - 1;

            for (int i = 0; i < nPoints; i++)
            {
                if (polygonVertices[i].m_dCoordinate_Y < this.m_dCoordinate_Y && polygonVertices[j].m_dCoordinate_Y >= this.m_dCoordinate_Y
                || polygonVertices[j].m_dCoordinate_Y < this.m_dCoordinate_Y && polygonVertices[i].m_dCoordinate_Y >= this.m_dCoordinate_Y)
                {
                    if (polygonVertices[i].m_dCoordinate_X + (this.m_dCoordinate_Y - polygonVertices[i].m_dCoordinate_Y) / (polygonVertices[j].m_dCoordinate_Y - polygonVertices[i].m_dCoordinate_Y) * (polygonVertices[j].m_dCoordinate_X - polygonVertices[i].m_dCoordinate_X) < this.m_dCoordinate_X)
                    {
                        oddNodes = !oddNodes;
                    }
                }
                j = i;
            }

            return oddNodes;
        }

        /*********** Sort points from Xmin->Xmax ******/
        public static void SortPointsByX(CPoint2D[] points)
		{
			if (points.Length>1)
			{
				CPoint2D tempPt;
				for (int i=0; i< points.Length-2; i++)
				{
					for (int j = i+1; j < points.Length -1; j++)
					{
						if (points[i].X > points[j].X)
						{
							tempPt= points[j];
							points[j]=points[i];
							points[i]=tempPt;
						}
					}
				}
			}
		}

		/*********** Sort points from Ymin->Ymax ******/
		public static void SortPointsByY(CPoint2D[] points)
		{
			if (points.Length>1)
			{
				CPoint2D tempPt;
				for (int i=0; i< points.Length-2; i++)
				{
					for (int j = i+1; j < points.Length -1; j++)
					{
						if (points[i].Y > points[j].Y)
						{
							tempPt= points[j];
							points[j]=points[i];
							points[i]=tempPt;
						}
					}
				}
			}
		}

	}
}
