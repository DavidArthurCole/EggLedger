package main

import "flag"

var (
	_replacePID  int
	_replacePath string
)

func init() {
	flag.IntVar(&_replacePID, "replace-pid", 0, "")
	flag.StringVar(&_replacePath, "replace-path", "", "")
}
