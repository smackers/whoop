// 
// Copyright (c) 2014 Pantazis Deligiannis (p.deligiannis@imperial.ac.uk)
// This file is distributed under the MIT License. See LICENSE for details.
// 

#include "clang/AST/AST.h"
#include "clang/AST/ASTConsumer.h"
#include "clang/AST/RecursiveASTVisitor.h"
#include "clang/AST/Attr.h"
#include "clang/Frontend/CompilerInstance.h"
#include "clang/Frontend/FrontendPluginRegistry.h"
#include "clang/Rewrite/Frontend/Rewriters.h"
#include "clang/Rewrite/Core/Rewriter.h"
#include "llvm/Support/raw_ostream.h"

using namespace clang;
using namespace std;

namespace {
	
class DriverInfo {
public:
	static DriverInfo& getInstance() {
		static DriverInfo instance;
		return instance;
	}
	
	void AddEntryPoint(string type, string funcname, string entrypoint) {
		entry_points[type][funcname] = entrypoint;
	}
	
	bool existsEntryPointWithName(string name) {
		bool exists = false;

		for(map<string, map<string, string> >::iterator i = entry_points.begin(); i != entry_points.end(); i++) {
			for(map<string, string>::iterator j = i->second.begin(); j != i->second.end(); j++) {
				if (name == j->second) {
					exists = true;
					break;
				}
			}
			
			if (exists) break;
		}
		
		return exists;
	}
	
	void PrintDriverInfo() {
		string file = fileName;
		file.append(".info");
		string error_msg;
		llvm::raw_fd_ostream *FOS = new llvm::raw_fd_ostream(file.c_str(), error_msg, llvm::sys::fs::F_None);
		if (!error_msg.empty()) {
	    if (llvm::errs().has_colors()) llvm::errs().changeColor(llvm::raw_ostream::RED);
	    llvm::errs() << "error: " << error_msg << "\n";
	    if (llvm::errs().has_colors()) llvm::errs().resetColor();
	    exit(1);
	  }
		
		FOS->SetUnbuffered();
		FOS->SetUseAtomicWrites(true);
		
		string output = "";
		
		for(map<string, map<string, string> >::iterator i = entry_points.begin(); i != entry_points.end(); i++) {
			output.append("<" + i->first + ">\n");
						
			for(map<string, string>::iterator j = i->second.begin(); j != i->second.end(); j++) {
				output.append(j->first + "::" +j->second + "\n");
			}
			
			output.append("</>\n");
		}
		
		raw_ostream *ros = FOS;
		
		ros->write(output.data(), output.size());
	}
	
	void SetFile(string fn) {
		this->fileName = fn;
	}
	
	string GetFile() {
		return fileName;
	}
	
private:
	string fileName;
	map<string, map<string, string> > entry_points;
	
	DriverInfo() {}
	DriverInfo(DriverInfo const&);
	void operator=(DriverInfo const&);
};

class RemoveStaticVisitor : public RecursiveASTVisitor<RemoveStaticVisitor> {
public:
  RemoveStaticVisitor(CompilerInstance &CI)
	  : Instance(CI) {
	  	RW.setSourceMgr(Instance.getSourceManager(), Instance.getLangOpts());
	  }

	bool VisitFunctionDecl(FunctionDecl* FD) {
		string fdFileWithExt = Instance.getSourceManager().getFilename(FD->getLocation());
		string fdFile = fdFileWithExt.substr(0, fdFileWithExt.find_last_of("."));
		
		if (DI->getInstance().existsEntryPointWithName(FD->getNameInfo().getName().getAsString())) {			
			if (FD->getStorageClass() == SC_Static) {
				RW.RemoveText(FD->getInnerLocStart(), 7);
			}
		} else if ((fdFile.size() > 0) && (DI->getInstance().GetFile().find(fdFile) != string::npos)) {
			if (FD->getStorageClass() == SC_Static)
				RW.ReplaceText(FD->getInnerLocStart(), 6, "static inline");
		}
		
		return true;
	}
	
	void Finalise() {
		string file = DI->getInstance().GetFile();
		file.append(".re.c");
		
		string error_msg;
		llvm::raw_fd_ostream *FOS = new llvm::raw_fd_ostream(file.c_str(), error_msg, llvm::sys::fs::F_None);
		if (!error_msg.empty()) {
	    if (llvm::errs().has_colors()) llvm::errs().changeColor(llvm::raw_ostream::RED);
	    llvm::errs() << "error: " << error_msg << "\n";
	    if (llvm::errs().has_colors()) llvm::errs().resetColor();
	    exit(1);
	  }
		
		FOS->SetUnbuffered();
		FOS->SetUseAtomicWrites(true);
		
		raw_ostream *ros = FOS;
		
		RW.getEditBuffer(RW.getSourceMgr().getMainFileID()).write(*ros);
	}
	
private:
  CompilerInstance &Instance;
	Rewriter RW;
	DriverInfo *DI;
};

class FindEntryPointsVisitor : public RecursiveASTVisitor<FindEntryPointsVisitor> {
public:
  FindEntryPointsVisitor() {}

	bool VisitVarDecl(VarDecl* VD) {
		if (!VD->getType()->isRecordType()) return true;
		
		RecordDecl *BaseRD = VD->getType()->getAs<RecordType>()->getDecl();
		
		if (!(BaseRD->getNameAsString() == "pci_driver" || BaseRD->getNameAsString() == "ethtool_ops" ||
				BaseRD->getNameAsString() == "test_driver")) {
			return true;
		}
		
		InitListExpr *ILE = cast<InitListExpr>(VD->getInit())->getSyntacticForm();
		
		for (Stmt::child_range range = ILE->children(); range; ++range) {				
			DesignatedInitExpr *DIE = cast<DesignatedInitExpr>(*range);				
			if (DIE->size() != 1) continue;
			
			string funcname;
			
			if (DIE->getDesignator(0)->getFieldName()->getName() == "probe" ||
				  DIE->getDesignator(0)->getFieldName()->getName() == "remove" ||
					DIE->getDesignator(0)->getFieldName()->getName() == "shutdown" ||
					DIE->getDesignator(0)->getFieldName()->getName() == "get_settings" ||
					DIE->getDesignator(0)->getFieldName()->getName() == "set_settings" ||
				  DIE->getDesignator(0)->getFieldName()->getName() == "probe" ||
					DIE->getDesignator(0)->getFieldName()->getName() == "ep1" ||
					DIE->getDesignator(0)->getFieldName()->getName() == "ep2" ||
					DIE->getDesignator(0)->getFieldName()->getName() == "ep2" ||
					DIE->getDesignator(0)->getFieldName()->getName() == "ep4" ||
					DIE->getDesignator(0)->getFieldName()->getName() == "ep5")
				funcname = DIE->getDesignator(0)->getFieldName()->getName();
			else
				continue;
			
			Expr *expr = cast<ImplicitCastExpr>(DIE->getInit())->getSubExpr();
			while (!isa<DeclRefExpr>(expr))
				expr = cast<ImplicitCastExpr>(expr)->getSubExpr();
			DeclRefExpr *DRE = cast<DeclRefExpr>(expr);
			
			DI->getInstance().AddEntryPoint(BaseRD->getNameAsString(), funcname, DRE->getNameInfo().getName().getAsString());
		}
		
		return true;
	}
	
	void PrintEntryPoints() {
		DI->getInstance().PrintDriverInfo();
	}
	
private:
	DriverInfo *DI;
};

class ParseDriverConsumer : public ASTConsumer {
public:
  explicit ParseDriverConsumer(CompilerInstance &CI)
    : FEPV(), RSV(CI) {}

  virtual void HandleTranslationUnit(ASTContext &AT) {
    FEPV.TraverseDecl(AT.getTranslationUnitDecl());
		FEPV.PrintEntryPoints();
		RSV.TraverseDecl(AT.getTranslationUnitDecl());
		RSV.Finalise();
  }
	
private:
  FindEntryPointsVisitor FEPV;
	RemoveStaticVisitor RSV;
};

class ParseDriverASTAction : public PluginASTAction {
protected:
  ASTConsumer *CreateASTConsumer(CompilerInstance &CI, llvm::StringRef) {
    return new ParseDriverConsumer(CI);
  }

  bool ParseArgs(const CompilerInstance &CI, const vector<string> &args) {		
		for (unsigned i = 0, e = args.size(); i != e; ++i) {
			if (args[i] == "help") {
	      PrintHelp(llvm::errs());
	      return false;
			} else if (args[i] == "filename") {
				++i;
				DI->getInstance().SetFile(args[i]);
			} else {
	      DiagnosticsEngine &D = CI.getDiagnostics();
	      unsigned DiagID = D.getCustomDiagID(
	          DiagnosticsEngine::Error, "invalid argument '%0'");
	      D.Report(DiagID) << args[0];
	      return false;
			}
		}
		
    return true;
  }

  void PrintHelp(llvm::raw_ostream &ros) {
    ros << "Front end for analysing Linux device drivers\n";
  }
	
private:
	DriverInfo *DI;
};

}

static FrontendPluginRegistry::Add<ParseDriverASTAction> X("chauffeur", "front end for analysing Linux device drivers");
