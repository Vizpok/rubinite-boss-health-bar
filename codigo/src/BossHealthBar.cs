// BossHealthBar: la barra de vida de los jefes de Rubinite se ve siempre.
//
// Después de terminar la primera vuelta, BossUI.Start() oculta la barra de los jefes:
//
//     switch (GamePlayCore.challengeMode) {
//         case None:          alwaysHide = isFirstRunFinished ? enableSecondRunHide : false; break;
//         case FirstRun_Ver:  alwaysHide = false; break;
//         case SecondRun_Ver: alwaysHide = true;  break;
//     }
//
// El parche cambia `ldarg.0; ldfld enableSecondRunHide` por `ldc.i4.0` (+ nop) y el `ldc.i4.1` del último
// caso por `ldc.i4.0`, así que alwaysHide siempre queda en false. Son 7 bytes que se cambian en su sitio
// (Mono.Cecil solo se usa para leer dónde está el método), así que no pisa otros mods.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Win32;

[assembly: AssemblyTitle("BossHealthBar")]
[assembly: AssemblyDescription("Mod para Rubinite: la barra de vida de los jefes se ve siempre")]
[assembly: AssemblyProduct("Rubinite Boss Health Bar")]
[assembly: AssemblyCompany("Vizpok")]
[assembly: AssemblyCopyright("Vizpok")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

static class Programa
{
    static bool conMenu;

    static int Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        try { return Ejecutar(args); }
        catch (Exception e)
        {
            Console.WriteLine();
            Console.WriteLine("Ocurrió un error: " + e.Message);
            Pausa();
            return 1;
        }
    }

    static int Ejecutar(string[] args)
    {
        Console.WriteLine("=== Rubinite: barra de vida de jefes siempre visible ===");
        Console.WriteLine();
        string juego = BuscarJuego(args.FirstOrDefault(a => !a.StartsWith("--")));
        if (juego == null)
        {
            Console.WriteLine("No encontré el juego. Arrastra la carpeta de Rubinite sobre el programa.");
            Pausa(); return 1;
        }
        Console.WriteLine("Juego: " + juego);
        Console.WriteLine();

        string accion = args.Contains("--instalar") ? "1" : args.Contains("--desinstalar") ? "2" : null;
        if (accion == null)
        {
            conMenu = true;
            Console.WriteLine("  1) Instalar");
            Console.WriteLine("  2) Desinstalar");
            Console.WriteLine();
            Console.Write("Elige 1 o 2 y presiona Enter: ");
            accion = (Console.ReadLine() ?? "").Trim();
            Console.WriteLine();
        }
        if (accion != "1" && accion != "2") { Console.WriteLine("Opción no válida."); Pausa(); return 1; }
        if (JuegoAbierto(juego))
        {
            Console.WriteLine("El juego está abierto. Ciérralo y vuelve a intentarlo.");
            Pausa(); return 1;
        }
        Parche.Aplicar(Path.Combine(juego, @"Rubinite_Data\Managed"), accion == "1");
        Pausa();
        return 0;
    }

    static void Pausa()
    {
        if (!conMenu) return;
        Console.WriteLine();
        Console.Write("Presiona Enter para cerrar...");
        Console.ReadLine();
    }

    static bool JuegoAbierto(string juego)
    {
        string esperado = Path.GetFullPath(Path.Combine(juego, "Rubinite.exe"));
        foreach (Process p in Process.GetProcessesByName("Rubinite"))
        {
            try { if (string.Equals(Path.GetFullPath(p.MainModule.FileName), esperado, StringComparison.OrdinalIgnoreCase)) return true; }
            catch { return true; }
        }
        return false;
    }

    static string BuscarJuego(string arg)
    {
        Func<string, bool> valido = c => c != "" && File.Exists(Path.Combine(c, @"Rubinite_Data\Managed\Assembly-CSharp.dll"));
        if (arg != null)   // si se indica una ruta, solo se usa esa
            return new[] { arg, Path.GetDirectoryName(arg) ?? "" }.FirstOrDefault(valido);
        List<string> raices = new List<string>();
        foreach (string clave in new[] { @"HKEY_CURRENT_USER\Software\Valve\Steam", @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam" })
            foreach (string valor in new[] { "SteamPath", "InstallPath" })
            {
                string v = Registry.GetValue(clave, valor, null) as string;
                if (!string.IsNullOrEmpty(v)) raices.Add(v.Replace('/', '\\'));
            }
        foreach (string raiz in raices.ToArray())
        {
            string vdf = Path.Combine(raiz, @"steamapps\libraryfolders.vdf");
            if (File.Exists(vdf))
                foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
                    raices.Add(m.Groups[1].Value.Replace(@"\\", @"\"));
        }
        foreach (char u in "CDEFGHIJ")
        {
            raices.Add(u + @":\Program Files (x86)\Steam");
            raices.Add(u + @":\SteamLibrary");
            raices.Add(u + @":\Steam");
        }
        return raices.Select(r => Path.Combine(r, @"steamapps\common\Rubinite")).FirstOrDefault(valido);
    }
}

static class Parche
{
    // Posición en el archivo del código IL de BossUI.Start y tokens de los dos campos.
    static void Localizar(string dll, out int ini, out int fin, out int tokOcultar, out int tokSiempre)
    {
        var p = new Mono.Cecil.ReaderParameters { InMemory = true };
        using (var asm = Mono.Cecil.AssemblyDefinition.ReadAssembly(dll, p))
        {
            var tipo = asm.MainModule.GetType("BossUI");
            if (tipo == null) throw new Exception("No encontré BossUI (¿cambió el juego?).");
            var start = tipo.Methods.FirstOrDefault(m => m.Name == "Start" && !m.HasParameters);
            if (start == null || start.RVA == 0) throw new Exception("No encontré BossUI.Start (¿cambió el juego?).");
            tokOcultar = tipo.Fields.First(f => f.Name == "enableSecondRunHide").MetadataToken.ToInt32();
            tokSiempre = tipo.Fields.First(f => f.Name == "alwaysHide").MetadataToken.ToInt32();
            byte[] datos = File.ReadAllBytes(dll);
            int off = RvaAPosicion(datos, start.RVA);
            if ((datos[off] & 3) == 2) { ini = off + 1; fin = ini + (datos[off] >> 2); }          // cabecera "tiny"
            else { ini = off + (datos[off + 1] >> 4) * 4; fin = ini + BitConverter.ToInt32(datos, off + 4); }
        }
    }

    static int RvaAPosicion(byte[] pe, int rva)
    {
        int nt = BitConverter.ToInt32(pe, 0x3c);
        int secciones = BitConverter.ToUInt16(pe, nt + 6);
        int tamOpcional = BitConverter.ToUInt16(pe, nt + 20);
        int s = nt + 24 + tamOpcional;
        for (int i = 0; i < secciones; i++, s += 40)
        {
            int va = BitConverter.ToInt32(pe, s + 12), tam = BitConverter.ToInt32(pe, s + 8), raw = BitConverter.ToInt32(pe, s + 20);
            if (rva >= va && rva < va + tam) return rva - va + raw;
        }
        throw new Exception("RVA fuera de las secciones del archivo.");
    }

    static List<int> Buscar(byte[] d, int ini, int fin, byte[] patron)
    {
        var res = new List<int>();
        for (int i = ini; i + patron.Length <= fin; i++)
        {
            int k = 0;
            while (k < patron.Length && d[i + k] == patron[k]) k++;
            if (k == patron.Length) res.Add(i);
        }
        return res;
    }

    static byte[] B(params object[] partes)
    {
        var l = new List<byte>();
        foreach (object o in partes) { if (o is int) l.AddRange(BitConverter.GetBytes((int)o)); else l.AddRange((byte[])o); }
        return l.ToArray();
    }

    public static void Aplicar(string managed, bool instalar)
    {
        string ruta = Path.Combine(managed, "Assembly-CSharp.dll");
        int ini, fin, tOcultar, tSiempre;
        Localizar(ruta, out ini, out fin, out tOcultar, out tSiempre);
        byte[] d = File.ReadAllBytes(ruta);

        // 1) ldarg.0; ldfld enableSecondRunHide   <->   ldc.i4.0; nop x5   (seguido de br.s)
        byte[] orig1 = B(new byte[] { 0x02, 0x7b }, tOcultar), nuevo1 = { 0x16, 0, 0, 0, 0, 0 };
        // 2) ldarg.0; ldc.i4.1; stfld alwaysHide  <->   ldarg.0; ldc.i4.0; stfld alwaysHide
        byte[] orig2 = B(new byte[] { 0x02, 0x17, 0x7d }, tSiempre), nuevo2 = B(new byte[] { 0x02, 0x16, 0x7d }, tSiempre);

        var p1 = Buscar(d, ini, fin, orig1);
        var p2 = Buscar(d, ini, fin, orig2);
        bool original = p1.Count == 1 && p2.Count == 1;
        var q1 = Buscar(d, ini, fin, B(nuevo1, new byte[] { 0x2b }));
        var q2 = Buscar(d, ini, fin, nuevo2);   // el caso "false" ya existía una vez: el segundo es el nuestro
        bool parcheado = p1.Count == 0 && p2.Count == 0 && q1.Count == 1 && q2.Count == 2;
        if (!original && !parcheado)
            throw new Exception("El código de la barra de jefes no coincide con lo esperado (¿actualización del juego?).");

        if (instalar == parcheado)
        {
            Console.WriteLine(instalar ? "Ya estaba instalado." : "No estaba instalado; no hay nada que quitar.");
            return;
        }
        if (instalar) { Array.Copy(nuevo1, 0, d, p1[0], 6); Array.Copy(nuevo2, 0, d, p2[0], nuevo2.Length); }
        else { Array.Copy(orig1, 0, d, q1[0], 6); Array.Copy(orig2, 0, d, q2[1], orig2.Length); }
        File.WriteAllBytes(ruta + ".tmp_barra", d);
        File.Copy(ruta + ".tmp_barra", ruta, true);
        File.Delete(ruta + ".tmp_barra");
        Console.WriteLine(instalar ? "Listo: la barra de vida de los jefes se verá siempre."
                                   : "Listo: el juego vuelve a ocultar la barra como antes.");
    }
}
