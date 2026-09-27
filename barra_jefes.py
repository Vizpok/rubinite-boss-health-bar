"""Rubinite: mantener visible la barra de vida de los jefes.

Después de terminar la primera vuelta, BossUI.Start() oculta la barra de los jefes:

    switch (GamePlayCore.challengeMode) {
        case None:          alwaysHide = isFirstRunFinished ? enableSecondRunHide : false; break;
        case FirstRun_Ver:  alwaysHide = false; break;
        case SecondRun_Ver: alwaysHide = true;  break;
    }

El parche cambia `ldarg.0; ldfld enableSecondRunHide` por `ldc.i4.0` (+ nop) y el `ldc.i4.1` del
último caso por `ldc.i4.0`, así que alwaysHide siempre queda en false. Se modifica la DLL en su
sitio y se puede revertir byte a byte, sin pisar otros mods que toquen el mismo archivo.

Uso:  BarraJefes.exe  (menú)       BarraJefes.exe [--instalar | --desinstalar] [ruta_del_juego]
"""
import os, re, struct, sys

CONGELADO = getattr(sys, 'frozen', False)
RUTA_DLL = ('Rubinite_Data', 'Managed', 'Assembly-CSharp.dll')


def bibliotecas_steam():
    raices = []
    try:
        import winreg
        for hive, clave in ((winreg.HKEY_CURRENT_USER, r'Software\Valve\Steam'),
                            (winreg.HKEY_LOCAL_MACHINE, r'SOFTWARE\WOW6432Node\Valve\Steam')):
            try:
                with winreg.OpenKey(hive, clave) as k:
                    for valor in ('SteamPath', 'InstallPath'):
                        try:
                            raices.append(os.path.normpath(winreg.QueryValueEx(k, valor)[0]))
                        except OSError:
                            pass
            except OSError:
                pass
    except ImportError:
        pass
    for raiz in list(raices):
        vdf = os.path.join(raiz, 'steamapps', 'libraryfolders.vdf')
        if os.path.isfile(vdf):
            texto = open(vdf, encoding='utf-8', errors='replace').read()
            raices += [p.replace('\\\\', '\\') for p in re.findall(r'"path"\s+"([^"]+)"', texto)]
    for unidad in 'CDEFGHIJ':
        raices += [unidad + r':\Program Files (x86)\Steam', unidad + r':\SteamLibrary', unidad + r':\Steam']
    return raices


def buscar_juego(arg):
    if arg:                                # si se indica una ruta, solo se usa esa
        candidatos = [arg, os.path.dirname(arg)]
    else:
        candidatos = [os.path.join(b, 'steamapps', 'common', 'Rubinite') for b in bibliotecas_steam()]
    for c in candidatos:
        if c and os.path.isfile(os.path.join(c, *RUTA_DLL)):
            return c
    salir('No encontré el juego. Arrastra la carpeta de Rubinite sobre el programa, '
          'o pásala como argumento: BarraJefes.exe "D:\\...\\Rubinite"')


def salir(mensaje=None, codigo=1):
    if mensaje:
        print(mensaje)
    if CONGELADO:
        input('\nPresiona Enter para cerrar...')
    sys.exit(codigo if mensaje else 0)


def cuerpo_metodo(dll, clase, metodo):
    """(inicio, fin) del código IL de clase::metodo dentro del archivo."""
    import dnfile
    pe = dnfile.dnPE(data=bytes(dll))
    for td in pe.net.mdtables.TypeDef:
        if str(td.TypeName) != clase:
            continue
        campos = {str(f.row.Name): 0x04000000 | f.row_index for f in td.FieldList}
        for m in td.MethodList:
            if str(m.row.Name) == metodo:
                off = pe.get_offset_from_rva(m.row.Rva)
                if dll[off] & 3 == 2:                                  # cabecera "tiny"
                    return off + 1, off + 1 + (dll[off] >> 2), campos
                tam_cab = (dll[off + 1] >> 4) * 4                      # cabecera "fat"
                tam = struct.unpack_from('<I', dll, off + 4)[0]
                return off + tam_cab, off + tam_cab + tam, campos
    raise RuntimeError(f'No encontré {clase}::{metodo} (¿cambió el juego?)')


def parches(dll):
    """Lista de (posición, bytes originales, bytes parcheados) para BossUI.Start."""
    ini, fin, campos = cuerpo_metodo(dll, 'BossUI', 'Start')
    t_ocultar = struct.pack('<I', campos['enableSecondRunHide'])
    t_siempre = struct.pack('<I', campos['alwaysHide'])
    cambios = [
        (b'\x02\x7b' + t_ocultar, b'\x16' + b'\x00' * 5),              # ldarg.0; ldfld enableSecondRunHide -> ldc.i4.0
        (b'\x02\x17\x7d' + t_siempre, b'\x02\x16\x7d' + t_siempre),     # alwaysHide = true -> false
    ]
    codigo = bytes(dll[ini:fin])
    res = []
    for original, nuevo in cambios:
        # el parche 1 deja la secuencia como "ldc.i4.0 + nop*5", que no es única: se busca el original y,
        # si ya no está, la versión parcheada que va justo antes de un br.s (0x2b)
        pos = [m.start() for m in re.finditer(re.escape(original), codigo)]
        estado = 'original'
        if not pos:
            patron = re.escape(nuevo) + (b'\x2b' if nuevo[0] == 0x16 else b'')
            pos = [m.start() for m in re.finditer(patron, codigo)]
            if original[1] == 0x17:           # el caso "false" ya existía una vez en el original: el segundo es el nuestro
                pos = pos[1:] if len(pos) == 2 else []
            estado = 'parcheado'
        if len(pos) != 1:
            raise RuntimeError('El código de la barra de jefes no coincide con lo esperado (¿actualización del juego?).')
        res.append((ini + pos[0], original, nuevo, estado))
    return res


def aplicar(juego, instalar):
    ruta = os.path.join(juego, *RUTA_DLL)
    dll = bytearray(open(ruta, 'rb').read())
    lista = parches(dll)
    objetivo = 'parcheado' if instalar else 'original'
    if all(e == objetivo for *_, e in lista):
        print('Ya estaba instalado.' if instalar else 'No estaba instalado; no hay nada que quitar.')
        return
    for pos, original, nuevo, _ in lista:
        dll[pos:pos + len(original)] = nuevo if instalar else original
    tmp = ruta + '.tmp_barra'
    open(tmp, 'wb').write(dll)
    os.replace(tmp, ruta)
    print('Listo: la barra de vida de los jefes se verá siempre.' if instalar
          else 'Listo: el juego vuelve a ocultar la barra como antes.')


def main():
    args = [a for a in sys.argv[1:] if not a.startswith('--')]
    accion = 'desinstalar' if '--desinstalar' in sys.argv else 'instalar' if '--instalar' in sys.argv else None
    print('=== Rubinite: barra de vida de jefes siempre visible ===\n')
    juego = buscar_juego(args[0] if args else None)
    print(f'Juego: {juego}\n')
    if accion is None:
        if not CONGELADO:
            accion = 'instalar'
        else:
            print('  1) Instalar')
            print('  2) Desinstalar')
            accion = {'1': 'instalar', '2': 'desinstalar'}.get(input('\nElige 1 o 2 y presiona Enter: ').strip())
            if not accion:
                salir('Opción no válida.')
            print()
    if os.name == 'nt':
        import subprocess
        if b'Rubinite.exe' in subprocess.run(['tasklist', '/FI', 'IMAGENAME eq Rubinite.exe'], capture_output=True).stdout:
            salir('El juego está abierto. Ciérralo y vuelve a intentarlo.')
    aplicar(juego, accion == 'instalar')
    salir(codigo=0)


if __name__ == '__main__':
    try:
        main()
    except SystemExit:
        raise
    except Exception as e:
        salir(f'\nOcurrió un error: {e}')
