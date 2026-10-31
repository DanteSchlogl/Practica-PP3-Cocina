using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

class Comanda
{
    public int Numero;
    public int Mesa;
    public string Plato;
    public string Mozo;

    public override string ToString()
    {
        return "#" + Numero + " Mesa " + Mesa + ": " + Plato;
    }
}

class Bandeja
{
    private readonly Queue<Comanda> comandas = new Queue<Comanda>();
    private readonly object candado = new object();
    private readonly int capacidad;
    private bool cerrada = false;

    public Bandeja(int capacidad) { this.capacidad = capacidad; }

    public void Dejar(Comanda c)
    {
        bool tomado = false;
        try
        {
            Monitor.Enter(candado, ref tomado);
            while (comandas.Count == capacidad)
            {
                Program.Log(c.Mozo + " espera: bandeja llena " + Ocupacion());
                Monitor.Wait(candado);
            }
            comandas.Enqueue(c);
            Program.Log(c.Mozo + " deja " + c + "  " + Ocupacion());
            Monitor.PulseAll(candado);
        }
        finally
        {
            if (tomado) Monitor.Exit(candado);
        }
    }

    public bool Tomar(string cocinero, out Comanda c)
    {
        bool tomado = false;
        try
        {
            Monitor.Enter(candado, ref tomado);
            while (comandas.Count == 0 && !cerrada)
            {
                Monitor.Wait(candado);
            }
            if (comandas.Count == 0)
            {
                c = null;
                return false;
            }
            c = comandas.Dequeue();
            Program.Log(cocinero + " toma " + c + "  " + Ocupacion());
            Monitor.PulseAll(candado);
            return true;
        }
        finally
        {
            if (tomado) Monitor.Exit(candado);
        }
    }

    public void Cerrar()
    {
        bool tomado = false;
        try
        {
            Monitor.Enter(candado, ref tomado);
            cerrada = true;
            Monitor.PulseAll(candado);
        }
        finally
        {
            if (tomado) Monitor.Exit(candado);
        }
    }

    private string Ocupacion()
    {
        return "[" + new string('#', comandas.Count) + new string('.', capacidad - comandas.Count) + "]";
    }
}

class Mozo
{
    private static readonly string[] Platos = { "Milanesa", "Ravioles", "Bife", "Ensalada", "Empanadas", "Pizza", "Flan" };
    private static int ultimoNumero = 0;

    private readonly string nombre;
    private readonly Bandeja bandeja;
    private readonly int cantidad;
    private readonly Random rnd;

    public Mozo(string nombre, Bandeja bandeja, int cantidad, int semilla)
    {
        this.nombre = nombre;
        this.bandeja = bandeja;
        this.cantidad = cantidad;
        rnd = new Random(semilla);
    }

    public void Ejecutar()
    {
        for (int i = 0; i < cantidad; i++)
        {
            Thread.Sleep(rnd.Next(150, 400));
            Comanda c = new Comanda();
            c.Numero = Interlocked.Increment(ref ultimoNumero);
            c.Mesa = rnd.Next(1, 16);
            c.Plato = Platos[rnd.Next(Platos.Length)];
            c.Mozo = nombre;
            bandeja.Dejar(c);
        }
        Program.Log(nombre + " terminó su turno");
    }
}

class Cocinero
{
    private readonly string nombre;
    private readonly Bandeja bandeja;
    private readonly Random rnd;
    public int Platos { get; set; }

    public Cocinero(string nombre, Bandeja bandeja, int semilla)
    {
        this.nombre = nombre;
        this.bandeja = bandeja;
        rnd = new Random(semilla);
    }

    public void Ejecutar()
    {
        Comanda c;
        while (bandeja.Tomar(nombre, out c))
        {
            Thread.Sleep(rnd.Next(400, 900));
            Platos = Platos + 1;
            Program.Log(nombre + " terminó " + c + " (de " + c.Mozo + ")");
        }
        Program.Log(nombre + " se va: cocinó " + Platos + " platos");
    }
}

class Program
{
    private static readonly Stopwatch reloj = Stopwatch.StartNew();

    public static void Log(string mensaje)
    {
        Console.WriteLine(reloj.Elapsed.TotalSeconds.ToString("0.00") + "s  " + mensaje);
    }

    static void Main()
    {
        Bandeja bandeja = new Bandeja(4);

        Mozo[] mozos =
        {
            new Mozo("Mozo Ana  ", bandeja, 5, 1),
            new Mozo("Mozo Beto ", bandeja, 5, 2),
            new Mozo("Mozo Carla", bandeja, 5, 3)
        };
        Cocinero[] cocineros =
        {
            new Cocinero("Cocinero Dani", bandeja, 4),
            new Cocinero("Cocinero Eli ", bandeja, 5)
        };

        // Task en vez de Thread: los hilos los maneja el ThreadPool.
        List<Task> hijosMozos = new List<Task>();
        List<Task> hijosCocineros = new List<Task>();
        foreach (Mozo m in mozos) hijosMozos.Add(Task.Run(m.Ejecutar));
        foreach (Cocinero k in cocineros) hijosCocineros.Add(Task.Run(k.Ejecutar));

        Task.WaitAll(hijosMozos.ToArray());
        bandeja.Cerrar();
        Task.WaitAll(hijosCocineros.ToArray());

        int total = 0;
        foreach (Cocinero k in cocineros) total = total + k.Platos;
        int esperado = mozos.Length * 5;
        Console.WriteLine();
        Console.WriteLine("Pedidos: " + esperado + " | Platos cocinados: " + total + " | " + (total == esperado ? "OK" : "ERROR: se perdieron pedidos"));
    }
}
