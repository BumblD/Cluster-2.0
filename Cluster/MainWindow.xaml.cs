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
            // Kai abi virsunes yra sandeliai
            graph.FindByValue("AT12").IsWarehouse = true;
            graph.FindByValue("AT11").IsWarehouse = true;
            FindCheapestPath(graph.FindByValue("AT12"), graph.FindByValue("AT11"), graph);
            // Kai pradzia sandelys, o pabaiga ne
            // REIKIA PRATESTINT!
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

        /// <summary>
        /// Finds cheapest route between two places
        /// </summary>
        /// <param name="Origin">Origin place</param>
        /// <param name="Destination">Destination place</param>
        /// <param name="Graph">Graph</param>
        void FindCheapestPath(GraphNode<string> Origin, GraphNode<string> Destination, Graph<string> Graph)
        {
            // Speju, kad dar reiktu labai optimizuot.. :D

            // jei abi virsunes sandeliai, tai tiesiog tikrinam ar pigiau su traukiniu gabent ar su sunkvezimiu
            if(Origin.IsWarehouse && Destination.IsWarehouse)
            {
                int key = FindCostsIndex(Origin, Destination);
                Console.WriteLine("From " + Origin.Value + " To " + Destination.Value);

                double RailCost = Graph.FindByValue(Destination.Value).RailCosts[key];
                double RoadCost = Graph.FindByValue(Destination.Value).RoadCosts[key];

                if (RailCost > RoadCost && RoadCost > 0)
                    Console.WriteLine("Cheaper by truck -> " + RoadCost);
                else if (RailCost < RoadCost && RailCost > 0)
                    Console.WriteLine("Cheaper by train -> " + RailCost);
                else if (RailCost == 0 && RoadCost == 0)
                    Console.WriteLine("There is no road from {0} to {1} !!!", Origin.Value, Destination.Value);
                else if (RailCost == RoadCost && (RoadCost > 0 || RailCost > 0))
                    Console.WriteLine("Same price for both vehicle types ->" + RoadCost);
            }
            // jei pradzia sandelys o pabaiga ne, reikia ieskot ar bent vienas is pabaigos kaimynu yra sandelys, jeigu taip, tikrinti ar 
            // imanoma is to sandelio nueit i pradzios sandeli, jeigu imanoma - sumuojam kaina traukinys+sunkvezimis(iki galutinio tasko)
            // ir lyginam su tiesioginio vezimo sunkvezimiu kaina is pradzios i galutini taska ir grazinam pigesni varianta, jei kuris nors
            // is ankstesniu if'u nepasiteisina, grazinam tiesioginio vezimo sunkvezimiu kaina is pradzios i galutini taska.
            else if (Origin.IsWarehouse && !Destination.IsWarehouse)
            {
                double PriceByTruck = 0;
                double PriceByTrainAndTruck = 9999999999;
                string TempWarehouse = null;

                // Surandame tiesioginio vezimo sunkvezimiu kaina
                int key = FindCostsIndex(Origin, Destination);
                PriceByTruck = Graph.FindByValue(Origin.Value).RoadCosts[key];

                // Ieskome ar imanoma krovini nugabent pigiau panaudojant papildoma sandeli (traukinio pagalba)
                for (int i = 0; i < Destination.Neighbors.Count; i++)
                {
                    double TempTrainCost = 0;
                    double TempTruckCost = 0;
                    double TempCost = 0;
                    //Tikriname ar bent vienas pabaigos kaimynas yra sandelys
                    GraphNode<string> Neighbour = Graph.FindByValue(Destination.Neighbors[i].Value);
                    if (Neighbour.IsWarehouse)
                    {
                        // Tikriname ar is pradzios tasko galima nugabenti krovini i pabaigos kaimyna
                        for (int j = 0; j < Origin.Neighbors.Count; j++)
                        {
                            if (Origin.Neighbors[j].Value == Neighbour.Value)
                            {
                                // Randame pervezimo kaina is pradzios tasko i tarpini sandeli
                                TempTrainCost = Neighbour.RailCosts[j];
                                break;
                            }
                        }
                        // Surandame pervezimo kaina sunkvezimiu is tarpinio sandelio i galutini taska
                        for (int j = 0; j < Destination.Neighbors.Count; j++)
                        {
                            if (Destination.Neighbors[j].Value == Neighbour.Value)
                            {
                                TempTruckCost = Graph.FindByValue(Destination.Neighbors[j].Value).RoadCosts[j];
                                break;
                            }
                        }
                        // Randame bendra kaina Traukinys(nuo pradzios iki tarpinio sandelio) + Sunkvezimis (Nuo tarpinio iki galutinio tasko)
                        if (TempTruckCost > 0 && TempTrainCost > 0)
                            TempCost = TempTruckCost + TempTrainCost;
                    }
                    // Ieskome pigiausio pervezimo naudojant tarpini sandeli
                    if (PriceByTrainAndTruck > TempCost && TempCost > 0)
                    {
                        PriceByTrainAndTruck = TempCost;
                        TempWarehouse = Neighbour.Value;
                    }
                }
                // Randame pigiausia pervezimo buda
                if (PriceByTruck == 0)
                    Console.WriteLine("Can't travel by truck!");
                else if (PriceByTrainAndTruck == 9999999999)
                    Console.WriteLine("Can't travel by train+truck!");
                else if (PriceByTruck < PriceByTrainAndTruck && PriceByTruck > 0)
                    Console.WriteLine("Cheaper by truck ->" + PriceByTruck);
                else if (PriceByTruck > PriceByTrainAndTruck && PriceByTrainAndTruck > 0)
                    Console.WriteLine("Cheaper by using temporary warehouse : {0}->{1}->{2} Price={3}", Origin.Value, TempWarehouse, Destination.Value, PriceByTrainAndTruck);

            }
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

        int FindCostsIndex(GraphNode<string> Origin, GraphNode<string> Destination)
        {
            for (int i = 0; i < Origin.Neighbors.Count; i++)
            {
                if (Origin.Neighbors[i].Value == Destination.Value)
                    return i;
            }
            return 999999;
        }
    }
}
