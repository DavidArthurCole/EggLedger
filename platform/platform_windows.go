//go:build windows

package platform

import (
	"path/filepath"
	"runtime"
	"syscall"
	"unsafe"

	"golang.org/x/sys/windows"
)

func Hide(path string) error {
	u16ptr, err := windows.UTF16PtrFromString(path)
	if err != nil {
		return err
	}
	return windows.SetFileAttributes(u16ptr, windows.FILE_ATTRIBUTE_HIDDEN)
}

func Show(path string) error {
	p, err := windows.UTF16PtrFromString(path)
	if err != nil {
		return err
	}
	attrs, err := windows.GetFileAttributes(p)
	if err != nil {
		return err
	}
	return windows.SetFileAttributes(p, attrs&^windows.FILE_ATTRIBUTE_HIDDEN)
}

func OpenFolderAndSelect(path string) error {
	abspath, _ := filepath.Abs(path)
	verb, _ := windows.UTF16PtrFromString("open")
	lpFile, _ := windows.UTF16PtrFromString("explorer.exe")
	lpParameters, _ := windows.UTF16PtrFromString("/select," + abspath)
	return windows.ShellExecute(0, verb, lpFile, lpParameters, nil, windows.SW_SHOW)
}

func Open(target string) error {
	verb, _ := windows.UTF16PtrFromString("open")
	var lpFile *uint16
	if len(target) >= 7 && (target[:7] == "http://" || (len(target) >= 8 && target[:8] == "https://")) {
		lpFile, _ = windows.UTF16PtrFromString(target)
	} else {
		abspath, err := filepath.Abs(target)
		if err != nil {
			return err
		}
		lpFile, _ = windows.UTF16PtrFromString(abspath)
	}
	return windows.ShellExecute(0, verb, lpFile, nil, nil, windows.SW_SHOW)
}

// COM support loaded from ole32.dll at runtime — CoCreateInstance is not in golang.org/x/sys/windows.
var (
	modOle32             = windows.NewLazySystemDLL("ole32.dll")
	procCoCreateInstance = modOle32.NewProc("CoCreateInstance")
	procCoTaskMemFree    = modOle32.NewProc("CoTaskMemFree")
)

// COM GUIDs for file dialogs.
var (
	clsidFileOpenDialog = windows.GUID{Data1: 0xDC1C5A9C, Data2: 0xE88A, Data3: 0x4DDE, Data4: [8]byte{0xA5, 0xA1, 0x60, 0xF8, 0x2A, 0x20, 0xAE, 0xF7}}
	iidIFileOpenDialog  = windows.GUID{Data1: 0xD57C7288, Data2: 0xD4AD, Data3: 0x4768, Data4: [8]byte{0xBE, 0x02, 0x9D, 0x96, 0x95, 0x32, 0xD9, 0x60}}
	clsidFileSaveDialog = windows.GUID{Data1: 0xC0B4E2F3, Data2: 0xBA21, Data3: 0x4773, Data4: [8]byte{0x8D, 0xBA, 0x33, 0x5E, 0xC9, 0x46, 0xEB, 0x8B}}
	iidIFileSaveDialog  = windows.GUID{Data1: 0x84BCCD23, Data2: 0x5FDE, Data3: 0x4CDB, Data4: [8]byte{0xAE, 0xA4, 0xAF, 0x64, 0xB8, 0x3D, 0x78, 0xAB}}
)

const (
	fosPickFolders     uint32  = 0x00000020
	sigdnFileSysPath   uintptr = 0x80058000
	clsctxInprocServer uint32  = 0x1
)

// iFileDialogVtbl covers the vtable for IUnknown + IModalWindow + IFileDialog.
// Offsets must match the Windows COM vtable layout exactly.
type iFileDialogVtbl struct {
	QueryInterface      uintptr // 0
	AddRef              uintptr // 1
	Release             uintptr // 2
	Show                uintptr // 3  IModalWindow::Show
	SetFileTypes        uintptr // 4
	SetFileTypeIndex    uintptr // 5
	GetFileTypeIndex    uintptr // 6
	Advise              uintptr // 7
	Unadvise            uintptr // 8
	SetOptions          uintptr // 9
	GetOptions          uintptr // 10
	SetDefaultFolder    uintptr // 11
	SetFolder           uintptr // 12
	GetFolder           uintptr // 13
	GetCurrentSelection uintptr // 14
	SetFileName         uintptr // 15
	GetFileName         uintptr // 16
	SetTitle            uintptr // 17
	SetOkButtonLabel    uintptr // 18
	SetFileNameLabel    uintptr // 19
	GetResult           uintptr // 20
	AddPlace            uintptr // 21
	SetDefaultExtension uintptr // 22
	Close               uintptr // 23
	SetClientGuid       uintptr // 24
	ClearClientData     uintptr // 25
	SetFilter           uintptr // 26
}

type iFileDialog struct {
	lpVtbl *iFileDialogVtbl
}

type iShellItemVtbl struct {
	QueryInterface uintptr // 0
	AddRef         uintptr // 1
	Release        uintptr // 2
	BindToHandler  uintptr // 3
	GetParent      uintptr // 4
	GetDisplayName uintptr // 5
	GetAttributes  uintptr // 6
	Compare        uintptr // 7
}

type iShellItem struct {
	lpVtbl *iShellItemVtbl
}

// comFilterSpec mirrors COMDLG_FILTERSPEC.
type comFilterSpec struct {
	pszName *uint16
	pszSpec *uint16
}

// showFileDialog is the shared implementation for folder/save dialogs.
func showFileDialog(clsid, iid *windows.GUID, extraOpts uint32, title, defaultFileName, filterName, filterSpec string) string {
	runtime.LockOSThread()
	defer runtime.UnlockOSThread()

	if err := windows.CoInitializeEx(0, windows.COINIT_APARTMENTTHREADED); err != nil {
		// S_FALSE (Errno 1) means already initialized on this thread — still OK.
		if errno, ok := err.(syscall.Errno); !ok || errno != 1 {
			return ""
		}
	}
	defer windows.CoUninitialize()

	var dlg *iFileDialog
	hr, _, _ := procCoCreateInstance.Call(
		uintptr(unsafe.Pointer(clsid)),
		0,
		uintptr(clsctxInprocServer),
		uintptr(unsafe.Pointer(iid)),
		uintptr(unsafe.Pointer(&dlg)),
	)
	if hr != 0 {
		return ""
	}
	defer syscall.Syscall(dlg.lpVtbl.Release, 1, uintptr(unsafe.Pointer(dlg)), 0, 0) //nolint:errcheck

	// Merge current options with the requested extra flags.
	var curOpts uint32
	syscall.Syscall(dlg.lpVtbl.GetOptions, 2, uintptr(unsafe.Pointer(dlg)), uintptr(unsafe.Pointer(&curOpts)), 0)
	syscall.Syscall(dlg.lpVtbl.SetOptions, 2, uintptr(unsafe.Pointer(dlg)), uintptr(curOpts|extraOpts), 0)

	if title != "" {
		titleU16, _ := windows.UTF16PtrFromString(title)
		syscall.Syscall(dlg.lpVtbl.SetTitle, 2, uintptr(unsafe.Pointer(dlg)), uintptr(unsafe.Pointer(titleU16)), 0)
	}

	if filterName != "" && filterSpec != "" {
		nameU16, _ := windows.UTF16PtrFromString(filterName)
		specU16, _ := windows.UTF16PtrFromString(filterSpec)
		filter := comFilterSpec{pszName: nameU16, pszSpec: specU16}
		syscall.Syscall(dlg.lpVtbl.SetFileTypes, 3, uintptr(unsafe.Pointer(dlg)), 1, uintptr(unsafe.Pointer(&filter)))
	}

	if defaultFileName != "" {
		fnU16, _ := windows.UTF16PtrFromString(defaultFileName)
		syscall.Syscall(dlg.lpVtbl.SetFileName, 2, uintptr(unsafe.Pointer(dlg)), uintptr(unsafe.Pointer(fnU16)), 0)
	}

	// Show the dialog. Non-zero HRESULT means cancelled or error.
	hr, _, _ = syscall.Syscall(dlg.lpVtbl.Show, 2, uintptr(unsafe.Pointer(dlg)), 0, 0)
	if hr != 0 {
		return ""
	}

	var item *iShellItem
	hr, _, _ = syscall.Syscall(dlg.lpVtbl.GetResult, 2, uintptr(unsafe.Pointer(dlg)), uintptr(unsafe.Pointer(&item)), 0)
	if hr != 0 {
		return ""
	}
	defer syscall.Syscall(item.lpVtbl.Release, 1, uintptr(unsafe.Pointer(item)), 0, 0) //nolint:errcheck

	var pathPtr *uint16
	hr, _, _ = syscall.Syscall(item.lpVtbl.GetDisplayName, 3, uintptr(unsafe.Pointer(item)), sigdnFileSysPath, uintptr(unsafe.Pointer(&pathPtr)))
	if hr != 0 {
		return ""
	}
	defer procCoTaskMemFree.Call(uintptr(unsafe.Pointer(pathPtr)))

	return windows.UTF16PtrToString(pathPtr)
}

// ChooseFolder opens a native Windows folder picker dialog.
// Returns the selected path, or "" if cancelled.
func ChooseFolder() string {
	return showFileDialog(&clsidFileOpenDialog, &iidIFileOpenDialog, fosPickFolders, "Choose Folder", "", "", "")
}

// ChooseSaveFilePath opens a native Windows save-file dialog.
// defaultName is the suggested filename. Returns the chosen path, or "" if cancelled.
func ChooseSaveFilePath(defaultName string) string {
	return showFileDialog(&clsidFileSaveDialog, &iidIFileSaveDialog, 0, "Save As", defaultName, "JSON files (*.json)", "*.json")
}
