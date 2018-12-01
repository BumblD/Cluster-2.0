using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;

namespace Cluster
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        const double WCF = 308525.05;  // warehouse construction cost fixed      eur
        const double WCV = 539.91;     // warehouse construction cost variable   eur/ton
        const double WMF = 8513.26;    // warehouse management cost fixed        eur/month
        const double WMV = 6.31;       // warehouse management cost variable     eur/ton/day
        const double TD = 125.41;      // truck delivery cost                    eur/ton/km
        const double RD = 3.95;        // railway delivery cost                  eur/ton/km
        const double TE = 0.062;       // truck emission CO2/km                  eur/km
        const double RE = 0.022;       // railway emission CO2/km                eur/km
        public MainWindow()
        {
            InitializeComponent();
            DataReader dr = new DataReader();
            HashSet<Distance> distances = dr.ReadDist();
            HashSet<Flow> flows = dr.ReadFlow();

            var railFlow = new HashSet<Flow>(flows.Where(x => x.Type.CompareTo("Rail") == 0).ToList());
            var roadFlow = new HashSet<Flow>(flows.Where(x => x.Type.CompareTo("Road") == 0).ToList());
            Console.WriteLine("{0} {1}", railFlow.Count, roadFlow.Count);

            double railCost = 0, roadCost = 0;
            foreach (var i in railFlow)
            {
                //delivery cost
                railCost += i.FlowTonKMs * RD;
                //CO2 cost
                railCost += i.FlowTonKMs * RE;
            }
            foreach (var i in roadFlow)
            {
                //delivery cost
                roadCost += i.FlowTonKMs * TD;
                //CO2 cost
                roadCost += i.FlowTonKMs * TE;
            }
            Console.WriteLine("{0} \n{1}", railCost, roadCost);

            //Grafo testas
            Graph<string> graph = new Graph<string>();
            graph = CreateGraph(distances, roadFlow, railFlow);

            using (StreamWriter writer = new StreamWriter("Test.txt"))
            {
                foreach (GraphNode<string> node in graph.Nodes)
                {
                    int i = 0;
                    writer.WriteLine(node.Value + " kaimynai:");
                    foreach (GraphNode<string> neib in node.Neighbors)
                    {
                        writer.WriteLine(neib.Value + " RoadFlow cost ->" + neib.RoadCosts[i] + " RailFlow cost ->" + neib.RailCosts[i]);
                        i++;
                    }
                }
            }
            // Pigiausio kelio testinimas
            graph.FindByValue("AT12").IsWarehouse = true;
            graph.FindByValue("AT11").IsWarehouse = true;
            // Kazkodel grazina atvirkscia reiksme (vietoj kelio AT11 -> AT12, grazina AT12 -> AT11)
            FindCheapestPath(graph.FindByValue("AT11"), graph.FindByValue("AT12"), graph);
        }

        Graph<string> CreateGraph(HashSet<Distance> distances, HashSet<Flow> roadFlows, HashSet<Flow> railFlows)
        {
            Graph<string> graph = new Graph<string>();

            foreach (var item in distances)
            {
                if (!graph.Contains(item.Origin))
                {
                    GraphNode<string> region = new GraphNode<string>(item.Origin);
                    graph.AddNode(region);
                }
            }

            foreach (var item in distances)
            {
                if (item.Origin != item.Destination)
                {
                    GraphNode<string> orig = graph.FindByValue(item.Origin);
                    GraphNode<string> dest = graph.FindByValue(item.Destination);
                    graph.AddEdge(orig, dest, TranspCostCalc(item.Dist, roadFlows, railFlows, orig, dest, true), TranspCostCalc(item.Dist, roadFlows, railFlows, orig, dest, false));
                    //graph.AddEdge(orig, dest, item.Dist, item.Dist);
                }
            }
            return graph;
        }

        //Reikia dar optimizuoti
        //kai IsRoadFlow = true skaiciuoja gabenimo sunkvezimiu kastus, false kai traukiniu
        double TranspCostCalc(double distance, HashSet<Flow> roadFlow, HashSet<Flow> railFlow, GraphNode<string> from, GraphNode<string> to, bool IsRoadFlow)
        {
            double transportationCost = 0;
            double totalCost = 0;
            if (IsRoadFlow)
            {
                //Flow road = roadFlow.FirstOrDefault(x => x.Load.Equals(from.Value) && x.Unload.Equals(to.Value));
                foreach (var item in roadFlow)
                {
                    if (item.Load == from.Value && item.Unload == to.Value)
                    {
                        transportationCost = item.FlowTonKMs * TD;
                        break;
                    }
                }
                /*if (road != null)
                    transportationCost = road.FlowTonKMs * TD;*/
                if (transportationCost > 0)
                    totalCost = distance * TE + transportationCost;
                
                return totalCost;
            }
            else
            {
                //Flow rail = railFlow.FirstOrDefault(x => x.Load.Equals(from.Value) && x.Unload.Equals(to.Value));
                foreach (var item in railFlow)
                {
                    if (item.Load == from.Value && item.Unload == to.Value)
                    {
                        transportationCost = item.FlowTonKMs * RD;
                        break;
                    }
                }
                /*if (rail != null)
                    transportationCost = rail.FlowTonKMs * RD;*/
                if (transportationCost > 0)
                    totalCost = distance * RE + transportationCost;

                return totalCost;
            }
        }

        void FindCheapestPath(GraphNode<string> Origin, GraphNode<string> Destination, Graph<string> graph)
        {
            // jei abi virsunes sandeliai, tai tiesiog tikrinam ar pigiau su traukiniu gabent ar su sunkvezimiu
            if(Origin.IsWarehouse && Destination.IsWarehouse)
            {
                int key = 999999;
                Console.WriteLine("From " + Origin.Value + " To " + Destination.Value);
                for (int i = 0; i < Origin.Neighbors.Count; i++)
                {
                    if (Origin.Neighbors[i].Value == Destination.Value)
                    {
                        key = i;
                        break;
                    }
                }

                if (graph.FindByValue(Origin.Value).RailCosts[key] < graph.FindByValue(Origin.Value).RoadCosts[key])
                    Console.WriteLine("Cheaper by truck -> " + graph.FindByValue(Origin.Value).RoadCosts[key]);
                else
                    Console.WriteLine("Cheaper by train -> " + graph.FindByValue(Origin.Value).RailCosts[key]);
            }
            // jei pradzia sandelys o pabaiga ne, reikia ieskot ar bent vienas is pabaigos kaimynu yra sandelys, jeigu taip, tikrinti ar 
            // imanoma is to sandelio nueit i pradzios sandeli, jeigu imanoma - sumuojam kaina traukinys+sunkvezimis(iki galutinio tasko)
            // ir lyginam su tiesioginio vezimo sunkvezimiu kaina is pradzios i galutini taska ir grazinam pigesni varianta, jei kuris nors
            // is ankstesniu if'u nepasiteisina, grazinam tiesioginio vezimo sunkvezimiu kaina is pradzios i galutini taska.

            // jei pradzia nera sandelys, o pabaiga yra, reikia ieskoti ar bent vienas is pradzios kaimynu yra sandelys, jeigu taip, tikrinti ar 
            // imanoma is to sandelio nueit i pabaigos sandeli, jeigu imanoma - sumuojam kaina sunkvezimis(iki tarpinio sandelio)+traukinys(iki tikslo)
            // ir lyginam su tiesioginio vezimo sunkvezimiu kaina is pradzios i galutini taska ir grazinam pigesni varianta, jei kuris nors
            // is ankstesniu if'u nepasiteisina, grazinam tiesioginio vezimo sunkvezimiu kaina is pradzios i galutini taska.

            // jei nei pradzia nei pabaiga nera sandelys, issirenkame ju kaimynus kurie yra sandeliai, tada tikriname ar is kaimyniniu pradzios
            // sandeliu imanoma nuvaziuot traukiniu i kaimyninius pabaigos sandelius, jeigu imanoma surandame visu galimu kelioniu kainas
            // sunkvezimis+traukinys+sunkvezimis ir isrenkame maziausia, tada lyginame su tiesioginio vezimo sunkvezimiu kaina is pradzios i 
            // galutini taska ir grazinam pigesni varianta, jei kuris nors is ankstesniu if'u nepasiteisina, grazinam tiesioginio vezimo sunkvezimiu kaina.

            // ir aisku paskutinis "else" grazina tiesioginio vezimo sunkvezimiu kaina.
        }
    }
}
