/*
* MIT License
* 
* Copyright(c) 2021 S4I s.r.l. (a MASES Group company)
* www.s4i.it www.masesgroup.com
* 
* Permission is hereby granted, free of charge, to any person obtaining a copy
* of this software and associated documentation files (the "Software"), to deal
* in the Software without restriction, including without limitation the rights
* to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
* copies of the Software, and to permit persons to whom the Software is
* furnished to do so, subject to the following conditions:
* 
* The above copyright notice and this permission notice shall be included in all
* copies or substantial portions of the Software.
* 
* THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
* IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
* FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
* AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
* LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
* OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
* SOFTWARE.
*/

using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;

// Le informazioni generali relative a un assembly sono controllate dal seguente 
// set di attributi. Modificare i valori di questi attributi per modificare le informazioni
// associate a un assembly.
[assembly: AssemblyTitle("ChatUI")]
[assembly: AssemblyDescription("ChatUI")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("S4I s.r.l.")]
[assembly: AssemblyProduct("CommunicationLib")]
[assembly: AssemblyCopyright("Copyright © 2021 S4I s.r.l.")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Se si imposta ComVisible su false, i tipi in questo assembly non saranno visibili 
// ai componenti COM. Se è necessario accedere a un tipo in questo assembly da 
// COM, impostare su true l'attributo ComVisible per tale tipo.
[assembly: ComVisible(false)]

//Per iniziare la compilazione delle applicazioni localizzabili, impostare 
//<UICulture>CultureYouAreCodingWith</UICulture> nel file .csproj
//all'interno di un <PropertyGroup>.  Ad esempio, se si utilizza l'inglese (Stati Uniti)
//nei file di origine, impostare <UICulture> su en-US.  Rimuovere quindi il commento dall'attributo
//NeutralResourceLanguage riportato di seguito.  Aggiornare "en-US" nella
//riga sottostante in modo che corrisponda all'impostazione UICulture nel file di progetto.

//[assembly: NeutralResourcesLanguage("en-US", UltimateResourceFallbackLocation.Satellite)]


[assembly: ThemeInfo(
    ResourceDictionaryLocation.None, //dove si trovano i dizionari delle risorse specifiche del tema
                                     //(in uso se non è possibile trovare una risorsa nella pagina 
                                     // oppure nei dizionari delle risorse dell'applicazione)
    ResourceDictionaryLocation.SourceAssembly //dove si trova il dizionario delle risorse generiche
                                              //(in uso se non è possibile trovare una risorsa nella pagina, 
                                              // nell'applicazione o nei dizionari delle risorse specifiche del tema)
)]


// Le informazioni sulla versione di un assembly sono costituite dai seguenti quattro valori:
//
//      Versione principale
//      Versione secondaria 
//      Numero di build
//      Revisione
//
// È possibile specificare tutti i valori oppure impostare valori predefiniti per i numeri relativi alla revisione e alla build 
// usando l'asterisco '*' come illustrato di seguito:
// [assembly: AssemblyVersion("1.0.*")]
[assembly: AssemblyVersion("0.5.0.0")]
[assembly: AssemblyFileVersion("0.5.0.0")]
